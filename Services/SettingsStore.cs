using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinDesk.Models;

namespace WinDesk.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _defaultPath;
    private readonly string _userPath;

    public SettingsStore()
        : this(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WinDesk",
                "appsettings.json"))
    {
    }

    public SettingsStore(string defaultPath, string userPath)
    {
        _defaultPath = defaultPath;
        _userPath = userPath;
    }

    public AppConfig Load()
    {
        var config = TryLoad(_userPath) ?? TryLoad(_defaultPath) ?? LoadEmbedded() ?? AppConfig.Default;
        var changed = false;

        // Migrate old password formats (plaintext / plain Base64) to XOR+Base64
        var plaintext = PasswordCrypto.Decode(config.LockPassword);
        var canonical = PasswordCrypto.Encode(plaintext);
        if (!string.Equals(canonical, config.LockPassword, StringComparison.Ordinal))
        {
            config.LockPassword = canonical;
            changed = true;
        }

        if (config.Markets.Count == 0)
        {
            config.Markets = AppConfig.Default.Markets;
            changed = true;
        }
        else if (AppendMissingDefaultMarkets(config))
        {
            changed = true;
        }

        if (config.GetCurrentMarket().Symbol != config.CurrentSymbol)
        {
            config.CurrentSymbol = config.GetCurrentMarket().Symbol;
            changed = true;
        }

        if (changed)
        {
            Save(config);
        }

        return config;
    }

    private static bool AppendMissingDefaultMarkets(AppConfig config)
    {
        var existingSymbols = new HashSet<string>(
            config.Markets.Select(market => market.Symbol),
            StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var market in AppConfig.Default.Markets)
        {
            if (existingSymbols.Add(market.Symbol))
            {
                config.Markets.Add(market);
                changed = true;
            }
        }

        return changed;
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_userPath)!);
        File.WriteAllText(_userPath, JsonSerializer.Serialize(config, JsonOptions));
    }

    private static AppConfig? TryLoad(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    // Fallback when no external appsettings.json is present (e.g. single-file publish):
    // read the config that was embedded into the assembly at build time.
    private static AppConfig? LoadEmbedded()
    {
        var assembly = typeof(SettingsStore).Assembly;
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return null;
        }

        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        try
        {
            return JsonSerializer.Deserialize<AppConfig>(reader.ReadToEnd(), JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
