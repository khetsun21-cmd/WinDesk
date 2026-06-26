using System.Windows.Media;

namespace MarketTicker.Models;

public sealed class AppConfig
{
    public int RefreshIntervalMs { get; set; } = 3000;
    public int RequestTimeoutMs { get; set; } = 2500;
    public string CurrentSymbol { get; set; } = "hf_GC";
    public string LockPassword { get; set; } = "a2hlMTk5MQ==";
    public WindowConfig Window { get; set; } = new();
    public List<MarketDefinition> Markets { get; set; } = [];

    public static AppConfig Default => new()
    {
        Markets =
        [
            new MarketDefinition
            {
                Name = "COMEX黄金",
                DisplayCode = "GC",
                Symbol = "hf_GC",
                UseRn = true,
                Referer = "https://finance.sina.com.cn/",
                Source = QuoteSource.Sina,
                Parser = QuoteParser.Global,
                Color = "D6B35A"
            },
            new MarketDefinition
            {
                Name = "WTI原油",
                DisplayCode = "CL",
                Symbol = "hf_CL",
                UseRn = true,
                Referer = "https://finance.sina.com.cn/",
                Source = QuoteSource.Sina,
                Parser = QuoteParser.Global,
                Color = "AAAAAA"
            },
            new MarketDefinition
            {
                Name = "中证1000",
                DisplayCode = "ZZ1000",
                Symbol = "s_sh000852",
                Referer = "https://vip.stock.finance.sina.com.cn/",
                Source = QuoteSource.Sina,
                Parser = QuoteParser.Index,
                Color = "AAAAAA"
            },
            new MarketDefinition
            {
                Name = "氧化铝主连",
                DisplayCode = "AO0",
                Symbol = "nf_AO0",
                UseRn = true,
                Referer = "https://vip.stock.finance.sina.com.cn/",
                Source = QuoteSource.Sina,
                Parser = QuoteParser.Futures,
                Color = "CCCCCC"
            },
            new MarketDefinition
            {
                Name = "豆粕主连",
                DisplayCode = "M0",
                Symbol = "nf_M0",
                UseRn = true,
                Referer = "https://vip.stock.finance.sina.com.cn/",
                Source = QuoteSource.Sina,
                Parser = QuoteParser.Futures,
                Color = "CCCCCC"
            }
        ]
    };

    public MarketDefinition GetCurrentMarket()
    {
        return Markets.FirstOrDefault(m => string.Equals(m.Symbol, CurrentSymbol, StringComparison.OrdinalIgnoreCase))
            ?? Markets.FirstOrDefault()
            ?? Default.Markets[0];
    }
}

public sealed class WindowConfig
{
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public bool Topmost { get; set; } = true;
}

public sealed class MarketDefinition
{
    public string Name { get; set; } = string.Empty;
    public string DisplayCode { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public bool UseRn { get; set; }
    public string Referer { get; set; } = string.Empty;
    public QuoteSource Source { get; set; } = QuoteSource.Sina;
    public QuoteParser Parser { get; set; } = QuoteParser.Global;
    public string Color { get; set; } = "FFFFFF";
    public string Url { get; set; } = string.Empty;
    public string SubscribeMessage { get; set; } = string.Empty;
    public string JsonPricePath { get; set; } = "data[0].last";
    public int? PriceFieldIndex { get; set; }

    public System.Windows.Media.Color ToBrushColor()
    {
        try
        {
            return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#" + Color.TrimStart('#'));
        }
        catch
        {
            return Colors.White;
        }
    }
}

public enum QuoteSource
{
    Sina,
    Okx,
    WebSocketJson
}

public enum QuoteParser
{
    Index,
    Futures,
    Global,
    OkxTicker
}
