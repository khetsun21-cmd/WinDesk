using System.Threading;
using WinDesk.Models;
using WinDesk.Services;
using Forms = System.Windows.Forms;

namespace WinDesk;

public partial class App
{
    private AppConfig _config = AppConfig.Default;
    private SettingsStore? _settingsStore;
    private QuoteService? _quoteService;
    private InputLockService? _lockService;
    private TrayIconController? _trayIconController;
    private MainWindow? _mainWindow;

    // Held for the whole process lifetime to enforce single-instance. It is static so the
    // GC never collects it mid-run (which would release the lock and let a second copy start).
    private static Mutex? _singleInstanceMutex;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        // Enforce a single running instance: if another copy already holds the named mutex,
        // exit this one right away so no second window / tray icon is created.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, name: @"Local\WinDesk_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            Shutdown(0);
            return;
        }

        base.OnStartup(e);

        Forms.Application.EnableVisualStyles();

        _settingsStore = new SettingsStore();
        _config = _settingsStore.Load();

        var password = PasswordCrypto.Decode(_config.LockPassword);
        _quoteService = new QuoteService(_config);
        _lockService = new InputLockService(password);
        _mainWindow = new MainWindow(_config, _settingsStore, _quoteService);
        _trayIconController = new TrayIconController(_mainWindow, _config.Markets, _lockService);

        _mainWindow.Show();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _lockService?.Dispose();
        _trayIconController?.Dispose();
        _quoteService?.Dispose();
        base.OnExit(e);
    }
}
