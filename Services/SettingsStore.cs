using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketTicker.Models;

namespace MarketTicker.Services;

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
    {
        _defaultPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        _userPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MarketTicker",
            "appsettings.json");
    }

    public AppConfig Load()
    {
        var config = TryLoad(_userPath) ?? TryLoad(_defaultPath) ?? AppConfig.Default;

        if (config.Markets.Count == 0)
        {
            config.Markets = AppConfig.Default.Markets;
        }

        if (config.GetCurrentMarket().Symbol != config.CurrentSymbol)
        {
            config.CurrentSymbol = config.GetCurrentMarket().Symbol;
        }

        return config;
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
}
