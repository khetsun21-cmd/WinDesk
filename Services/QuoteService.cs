using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MarketTicker.Models;

namespace MarketTicker.Services;

public sealed partial class QuoteService : IQuoteClient, IDisposable
{
    private readonly HttpClient _httpClient;

    public QuoteService(AppConfig config)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(Math.Max(500, config.RequestTimeoutMs))
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) MarketTicker/1.0");
        _httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
    }

    public async Task<Quote> GetLatestAsync(MarketDefinition market, CancellationToken cancellationToken)
    {
        return market.Source switch
        {
            QuoteSource.Okx => await GetOkxQuoteAsync(market, cancellationToken),
            QuoteSource.WebSocketJson => await GetWebSocketJsonQuoteAsync(market, cancellationToken),
            _ => await GetSinaQuoteAsync(market, cancellationToken)
        };
    }

    private async Task<Quote> GetSinaQuoteAsync(MarketDefinition market, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildSinaUrl(market));
        if (!string.IsNullOrWhiteSpace(market.Referer))
        {
            request.Headers.Referrer = new Uri(market.Referer);
        }

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var text = Encoding.GetEncoding("GB18030").GetString(bytes);
        var fields = ExtractSinaFields(text);
        var priceIndex = ResolvePriceFieldIndex(market);

        if (fields.Length <= priceIndex || string.IsNullOrWhiteSpace(fields[priceIndex]))
        {
            throw new InvalidOperationException($"Sina quote missing price field {priceIndex} for {market.Symbol}.");
        }

        var name = !string.IsNullOrWhiteSpace(fields[0]) && market.Parser != QuoteParser.Global ? fields[0] : market.Name;
        var price = FormatPrice(fields[priceIndex]);
        return new Quote(name, market.Symbol, price, DateTimeOffset.Now);
    }

    private async Task<Quote> GetOkxQuoteAsync(MarketDefinition market, CancellationToken cancellationToken)
    {
        var url = string.IsNullOrWhiteSpace(market.Url)
            ? $"https://www.okx.com/api/v5/market/ticker?instId={Uri.EscapeDataString(market.Symbol)}"
            : market.Url;

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var last = TryGetOkxLast(root);
        if (string.IsNullOrWhiteSpace(last))
        {
            throw new InvalidOperationException($"OKX quote missing last price for {market.Symbol}.");
        }

        return new Quote(market.Name, market.Symbol, FormatPrice(last), DateTimeOffset.Now);
    }

    private async Task<Quote> GetWebSocketJsonQuoteAsync(MarketDefinition market, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(market.Url))
        {
            throw new InvalidOperationException($"WebSocket URL is required for {market.Symbol}.");
        }

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(market.Url), cancellationToken);

        if (!string.IsNullOrWhiteSpace(market.SubscribeMessage))
        {
            var payload = Encoding.UTF8.GetBytes(market.SubscribeMessage);
            await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, cancellationToken);
        }

        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();

        while (!cancellationToken.IsCancellationRequested)
        {
            message.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new InvalidOperationException($"WebSocket closed before quote for {market.Symbol}.");
                }

                message.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            var text = Encoding.UTF8.GetString(message.ToArray());
            using var document = JsonDocument.Parse(text);
            var last = TryReadJsonPath(document.RootElement, market.JsonPricePath);
            if (!string.IsNullOrWhiteSpace(last))
            {
                return new Quote(market.Name, market.Symbol, FormatPrice(last), DateTimeOffset.Now);
            }
        }

        throw new OperationCanceledException(cancellationToken);
    }

    private static string BuildSinaUrl(MarketDefinition market)
    {
        if (!string.IsNullOrWhiteSpace(market.Url))
        {
            return market.Url;
        }

        var rn = market.UseRn ? $"rn={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}&" : string.Empty;
        return $"https://hq.sinajs.cn/{rn}list={Uri.EscapeDataString(market.Symbol)}";
    }

    private static string[] ExtractSinaFields(string text)
    {
        var match = SinaPayloadRegex().Match(text);
        if (!match.Success)
        {
            throw new InvalidOperationException("Sina response does not contain a quote payload.");
        }

        return match.Groups[1].Value.Split(',');
    }

    private static int ResolvePriceFieldIndex(MarketDefinition market)
    {
        if (market.PriceFieldIndex is int configured)
        {
            return configured;
        }

        return market.Parser switch
        {
            QuoteParser.Index => 1,
            QuoteParser.Futures => 6,
            QuoteParser.Global => 0,
            _ => 0
        };
    }

    private static string? TryGetOkxLast(JsonElement root)
    {
        return TryReadJsonPath(root, "data[0].last")
            ?? TryReadJsonPath(root, "last");
    }

    private static string? TryReadJsonPath(JsonElement root, string path)
    {
        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var propertyName = segment;
            int? index = null;
            var bracketStart = segment.IndexOf('[', StringComparison.Ordinal);
            if (bracketStart >= 0 && segment.EndsWith(']'))
            {
                propertyName = segment[..bracketStart];
                if (int.TryParse(segment[(bracketStart + 1)..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedIndex))
                {
                    index = parsedIndex;
                }
            }

            if (!string.IsNullOrWhiteSpace(propertyName))
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(propertyName, out current))
                {
                    return null;
                }
            }

            if (index is int arrayIndex)
            {
                if (current.ValueKind != JsonValueKind.Array || current.GetArrayLength() <= arrayIndex)
                {
                    return null;
                }

                current = current[arrayIndex];
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            _ => null
        };
    }

    private static string FormatPrice(string raw)
    {
        var trimmed = raw.Trim();
        if (!decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return trimmed;
        }

        return value switch
        {
            >= 1000m => value.ToString("0.0", CultureInfo.InvariantCulture),
            >= 100m => value.ToString("0.00", CultureInfo.InvariantCulture),
            _ => value.ToString("0.###", CultureInfo.InvariantCulture)
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [GeneratedRegex("=\\\"(.*)\\\";?", RegexOptions.Singleline)]
    private static partial Regex SinaPayloadRegex();
}
