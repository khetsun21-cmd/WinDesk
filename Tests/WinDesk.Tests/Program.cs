using WinDesk.Models;
using WinDesk.Services;

internal static class Program
{
    private static int Main()
    {
        var tests = new List<(string Name, Action Body)>
        {
            ("ApplyExcludeFromCapture uses WDA_EXCLUDEFROMCAPTURE for a valid handle", ApplyExcludeFromCaptureUsesExpectedAffinity),
            ("ApplyExcludeFromCapture ignores a zero handle", ApplyExcludeFromCaptureIgnoresZeroHandle),
            ("ClampToVisibleArea moves an offscreen window into a visible work area", ClampToVisibleAreaMovesOffscreenWindowIntoVisibleWorkArea),
            ("ClampToVisibleArea keeps a window inside the nearest work area", ClampToVisibleAreaKeepsWindowInsideNearestWorkArea),
            ("Default markets include Nasdaq 100 futures", DefaultMarketsIncludeNasdaq100Futures),
            ("Loading saved user config appends missing default markets", LoadingSavedUserConfigAppendsMissingDefaultMarkets),
            ("Main window keeps transparent chrome", MainWindowKeepsTransparentChrome)
        };

        var failures = 0;
        foreach (var test in tests)
        {
            try
            {
                test.Body();
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
            }
        }

        return failures == 0 ? 0 : 1;
    }

    private static void ApplyExcludeFromCaptureUsesExpectedAffinity()
    {
        var calls = new List<(IntPtr Handle, uint Affinity)>();
        var protector = new WindowCaptureProtector((handle, affinity) =>
        {
            calls.Add((handle, affinity));
            return true;
        });
        var handle = new IntPtr(1234);

        var applied = protector.ApplyExcludeFromCapture(handle);

        Assert(applied, "expected protector to report success");
        Assert(calls.Count == 1, $"expected one native call, got {calls.Count}");
        Assert(calls[0].Handle == handle, "expected the original window handle");
        Assert(calls[0].Affinity == WindowCaptureProtector.WdaExcludeFromCapture,
            $"expected affinity 0x{WindowCaptureProtector.WdaExcludeFromCapture:X}, got 0x{calls[0].Affinity:X}");
    }

    private static void ApplyExcludeFromCaptureIgnoresZeroHandle()
    {
        var calls = 0;
        var protector = new WindowCaptureProtector((_, _) =>
        {
            calls++;
            return true;
        });

        var applied = protector.ApplyExcludeFromCapture(IntPtr.Zero);

        Assert(!applied, "expected zero handle to be reported as not applied");
        Assert(calls == 0, $"expected no native calls for zero handle, got {calls}");
    }

    private static void ClampToVisibleAreaMovesOffscreenWindowIntoVisibleWorkArea()
    {
        var workAreas = new[]
        {
            new WindowWorkArea(0, 0, 1920, 1040),
            new WindowWorkArea(1920, 0, 1280, 984)
        };

        var placement = WindowPlacementService.ClampToVisibleArea(
            left: 4000,
            top: 1500,
            width: 118,
            height: 42,
            workAreas);

        Assert(placement.Left == 3082, $"expected left 3082, got {placement.Left}");
        Assert(placement.Top == 942, $"expected top 942, got {placement.Top}");
    }

    private static void ClampToVisibleAreaKeepsWindowInsideNearestWorkArea()
    {
        var workAreas = new[]
        {
            new WindowWorkArea(0, 0, 1920, 1040),
            new WindowWorkArea(1920, 0, 1280, 984)
        };

        var placement = WindowPlacementService.ClampToVisibleArea(
            left: 3180,
            top: -20,
            width: 118,
            height: 42,
            workAreas);

        Assert(placement.Left == 3082, $"expected left 3082, got {placement.Left}");
        Assert(placement.Top == 0, $"expected top 0, got {placement.Top}");
    }

    private static void DefaultMarketsIncludeNasdaq100Futures()
    {
        var market = AppConfig.Default.Markets.SingleOrDefault(m =>
            string.Equals(m.Symbol, "hf_NQ", StringComparison.OrdinalIgnoreCase));

        Assert(market is not null, "expected default markets to include hf_NQ");
        Assert(market.Name == "纳斯达克100期货", $"expected NQ market name, got {market.Name}");
        Assert(market.DisplayCode == "NQ", $"expected NQ display code, got {market.DisplayCode}");
        Assert(market.Source == QuoteSource.Sina, $"expected Sina source, got {market.Source}");
        Assert(market.Parser == QuoteParser.Global, $"expected global parser, got {market.Parser}");
    }

    private static void LoadingSavedUserConfigAppendsMissingDefaultMarkets()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "WinDesk.Tests", Guid.NewGuid().ToString("N"));

        try
        {
            var defaultPath = Path.Combine(tempRoot, "default.json");
            var userPath = Path.Combine(tempRoot, "WinDesk", "appsettings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(userPath)!);
            File.WriteAllText(userPath, """
                {
                  "refreshIntervalMs": 3000,
                  "requestTimeoutMs": 2500,
                  "currentSymbol": "hf_GC",
                  "lockPassword": "MVQbLqJ08w==",
                  "window": {
                    "left": 100,
                    "top": 100,
                    "topmost": true
                  },
                  "markets": [
                    {
                      "name": "COMEX黄金",
                      "displayCode": "GC",
                      "symbol": "hf_GC",
                      "useRn": true,
                      "referer": "https://finance.sina.com.cn/",
                      "source": "sina",
                      "parser": "global",
                      "color": "D6B35A"
                    }
                  ]
                }
                """);

            var store = new SettingsStore(defaultPath, userPath);
            var config = store.Load();

            Assert(config.Markets.Any(m => string.Equals(m.Symbol, "hf_NQ", StringComparison.OrdinalIgnoreCase)),
                "expected loaded config to append hf_NQ from default markets");

            var saved = File.ReadAllText(userPath);
            Assert(saved.Contains("\"symbol\": \"hf_NQ\"", StringComparison.Ordinal),
                "expected migrated user config to be saved with hf_NQ");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void MainWindowKeepsTransparentChrome()
    {
        var xaml = File.ReadAllText(FindRepoFile("MainWindow.xaml"));

        Assert(xaml.Contains("AllowsTransparency=\"True\"", StringComparison.Ordinal),
            "expected window AllowsTransparency to remain enabled");
        Assert(xaml.Contains("Background=\"Transparent\"", StringComparison.Ordinal),
            "expected transparent backgrounds instead of opaque fallback colors");
        Assert(!xaml.Contains("Background=\"#01000000\"", StringComparison.Ordinal),
            "expected no near-transparent black background because it renders as a black rectangle");
    }

    private static string FindRepoFile(string fileName)
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, fileName);
            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find {fileName} from {Environment.CurrentDirectory}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
