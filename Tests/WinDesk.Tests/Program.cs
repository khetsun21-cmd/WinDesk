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
            ("MoveProportionallyToVisibleArea preserves relative work area position", MoveProportionallyToVisibleAreaPreservesRelativeWorkAreaPosition),
            ("Default markets include configured quote symbols", DefaultMarketsIncludeConfiguredQuoteSymbols),
            ("Default IM0 uses the CFFEX latest price field", DefaultIm0UsesCffexLatestPriceField),
            ("Loading saved user config appends missing default markets", LoadingSavedUserConfigAppendsMissingDefaultMarkets),
            ("Loading saved IM0 config applies the default price field", LoadingSavedIm0ConfigAppliesDefaultPriceField),
            ("Loading saved IM0 config preserves an explicit price field", LoadingSavedIm0ConfigPreservesExplicitPriceField),
            ("Main window keeps transparent chrome", MainWindowKeepsTransparentChrome),
            ("Loaded transparent window does not enable capture exclusion", LoadedTransparentWindowDoesNotEnableCaptureExclusion),
            ("Display settings handler uses proportional placement", DisplaySettingsHandlerUsesProportionalPlacement),
            ("Topmost timer does not poll window placement", TopmostTimerDoesNotPollWindowPlacement)
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

    private static void MoveProportionallyToVisibleAreaPreservesRelativeWorkAreaPosition()
    {
        var previousArea = new WindowWorkArea(0, 0, 1920, 1040);
        var currentAreas = new[]
        {
            new WindowWorkArea(0, 0, 1280, 984)
        };

        var placement = WindowPlacementService.MoveProportionallyToVisibleArea(
            left: 960,
            top: 520,
            width: 120,
            height: 40,
            previousArea,
            currentAreas);

        AssertApproximately(618.6666666667, placement.Left, "expected left to keep the same relative X position");
        AssertApproximately(490.88, placement.Top, "expected top to keep the same relative Y position");
    }

    private static void DefaultMarketsIncludeConfiguredQuoteSymbols()
    {
        AssertDefaultMarket("hf_NQ", "纳斯达克100期货", "NQ", QuoteParser.Global);
        AssertDefaultMarket("nf_BR2609", "合成橡胶2609", "BR2609", QuoteParser.Futures);
        AssertDefaultMarket("nf_PG0", "LPG主连", "PG0", QuoteParser.Futures);
    }

    private static void DefaultIm0UsesCffexLatestPriceField()
    {
        var market = AppConfig.Default.Markets.Single(m =>
            string.Equals(m.Symbol, "nf_IM0", StringComparison.OrdinalIgnoreCase));

        Assert(market.PriceFieldIndex == 3,
            $"expected IM0 price field 3, got {market.PriceFieldIndex?.ToString() ?? "null"}");
    }

    private static void AssertDefaultMarket(string symbol, string name, string displayCode, QuoteParser parser)
    {
        var market = AppConfig.Default.Markets.SingleOrDefault(m =>
            string.Equals(m.Symbol, symbol, StringComparison.OrdinalIgnoreCase));

        if (market is null)
        {
            throw new InvalidOperationException($"expected default markets to include {symbol}");
        }

        Assert(market.Name == name, $"expected {displayCode} market name, got {market.Name}");
        Assert(market.DisplayCode == displayCode, $"expected {displayCode} display code, got {market.DisplayCode}");
        Assert(market.Source == QuoteSource.Sina, $"expected Sina source, got {market.Source}");
        Assert(market.Parser == parser, $"expected {parser} parser, got {market.Parser}");
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
            Assert(config.Markets.Any(m => string.Equals(m.Symbol, "nf_BR2609", StringComparison.OrdinalIgnoreCase)),
                "expected loaded config to append nf_BR2609 from default markets");

            var saved = File.ReadAllText(userPath);
            Assert(saved.Contains("\"symbol\": \"hf_NQ\"", StringComparison.Ordinal),
                "expected migrated user config to be saved with hf_NQ");
            Assert(saved.Contains("\"symbol\": \"nf_BR2609\"", StringComparison.Ordinal),
                "expected migrated user config to be saved with nf_BR2609");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void LoadingSavedIm0ConfigAppliesDefaultPriceField()
    {
        WithSavedUserConfig(
            """
            {
              "currentSymbol": "nf_IM0",
              "lockPassword": "MVQbLqJ08w==",
              "markets": [
                {
                  "name": "中千主连",
                  "displayCode": "IM0",
                  "symbol": "nf_IM0",
                  "source": "sina",
                  "parser": "futures"
                }
              ]
            }
            """,
            (config, userPath) =>
            {
                var market = config.Markets.Single(m =>
                    string.Equals(m.Symbol, "nf_IM0", StringComparison.OrdinalIgnoreCase));

                Assert(market.PriceFieldIndex == 3,
                    $"expected saved IM0 price field to migrate to 3, got {market.PriceFieldIndex?.ToString() ?? "null"}");

                var saved = File.ReadAllText(userPath);
                Assert(saved.Contains("\"priceFieldIndex\": 3", StringComparison.Ordinal),
                    "expected migrated IM0 price field to be persisted");
            });
    }

    private static void LoadingSavedIm0ConfigPreservesExplicitPriceField()
    {
        WithSavedUserConfig(
            """
            {
              "currentSymbol": "nf_IM0",
              "lockPassword": "MVQbLqJ08w==",
              "markets": [
                {
                  "name": "中千主连",
                  "displayCode": "IM0",
                  "symbol": "nf_IM0",
                  "source": "sina",
                  "parser": "futures",
                  "priceFieldIndex": 9
                }
              ]
            }
            """,
            (config, userPath) =>
            {
                var market = config.Markets.Single(m =>
                    string.Equals(m.Symbol, "nf_IM0", StringComparison.OrdinalIgnoreCase));

                Assert(market.PriceFieldIndex == 9,
                    $"expected explicit IM0 price field 9, got {market.PriceFieldIndex?.ToString() ?? "null"}");

                var saved = File.ReadAllText(userPath);
                Assert(saved.Contains("\"priceFieldIndex\": 9", StringComparison.Ordinal),
                    "expected explicit IM0 price field to remain persisted");
            });
    }

    private static void WithSavedUserConfig(string json, Action<AppConfig, string> assertions)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "WinDesk.Tests", Guid.NewGuid().ToString("N"));

        try
        {
            var defaultPath = Path.Combine(tempRoot, "default.json");
            var userPath = Path.Combine(tempRoot, "WinDesk", "appsettings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(userPath)!);
            File.WriteAllText(userPath, json);

            var store = new SettingsStore(defaultPath, userPath);
            assertions(store.Load(), userPath);
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

    private static void LoadedTransparentWindowDoesNotEnableCaptureExclusion()
    {
        var source = File.ReadAllText(FindRepoFile("MainWindow.xaml.cs"));
        var method = ExtractMethod(source, "Window_Loaded");

        Assert(!method.Contains("ApplyExcludeFromCapture", StringComparison.Ordinal),
            "expected transparent window loading to avoid capture exclusion because Windows renders it as a black block");
    }

    private static void DisplaySettingsHandlerUsesProportionalPlacement()
    {
        var source = File.ReadAllText(FindRepoFile("MainWindow.xaml.cs"));
        var method = ExtractMethod(source, "SystemEvents_DisplaySettingsChanged");

        Assert(method.Contains("MoveWindowProportionallyToScreen", StringComparison.Ordinal),
            "expected display settings changes to remap the window proportionally");
    }

    private static void TopmostTimerDoesNotPollWindowPlacement()
    {
        var source = File.ReadAllText(FindRepoFile("MainWindow.xaml.cs"));
        var method = ExtractMethod(source, "TopmostTimer_Tick");

        Assert(!method.Contains("EnsureWindowOnScreen", StringComparison.Ordinal),
            "expected topmost timer not to poll or save window placement");
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

    private static string ExtractMethod(string source, string methodName)
    {
        var start = source.IndexOf($"private void {methodName}", StringComparison.Ordinal);
        if (start < 0)
        {
            start = source.IndexOf($"private async void {methodName}", StringComparison.Ordinal);
        }

        if (start < 0)
        {
            throw new InvalidOperationException($"Could not find method {methodName}.");
        }

        var bodyStart = source.IndexOf('{', start);
        if (bodyStart < 0)
        {
            throw new InvalidOperationException($"Could not find body for method {methodName}.");
        }

        var depth = 0;
        for (var i = bodyStart; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source[bodyStart..(i + 1)];
                }
            }
        }

        throw new InvalidOperationException($"Could not extract body for method {methodName}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertApproximately(double expected, double actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.001)
        {
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
        }
    }
}
