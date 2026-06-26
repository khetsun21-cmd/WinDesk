using MarketTicker.Models;
using MarketTicker.Services;
using Forms = System.Windows.Forms;

namespace MarketTicker;

public partial class App
{
    private AppConfig _config = AppConfig.Default;
    private SettingsStore? _settingsStore;
    private QuoteService? _quoteService;
    private TrayIconController? _trayIconController;
    private MainWindow? _mainWindow;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        Forms.Application.EnableVisualStyles();

        _settingsStore = new SettingsStore();
        _config = _settingsStore.Load();

        _quoteService = new QuoteService(_config);
        _mainWindow = new MainWindow(_config, _settingsStore, _quoteService);
        _trayIconController = new TrayIconController(_mainWindow, _config.Markets);

        _mainWindow.Show();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _trayIconController?.Dispose();
        _quoteService?.Dispose();
        base.OnExit(e);
    }
}
