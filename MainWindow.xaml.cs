using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WinDesk.Interop;
using WinDesk.Models;
using WinDesk.Services;

namespace WinDesk;

public partial class MainWindow
{
    private readonly AppConfig _config;
    private readonly SettingsStore _settingsStore;
    private readonly QuoteService _quoteService;
    private readonly PeriodicTimer _refreshTimer;
    private readonly DispatcherTimer _topmostTimer;
    private readonly CancellationTokenSource _shutdown = new();
    private CancellationTokenSource? _refreshCts;
    private MarketDefinition _currentMarket;
    private bool _allowClose;
    private IntPtr _handle;

    public event EventHandler<MarketDefinition>? MarketChanged;
    public event EventHandler<string>? PriceChanged;

    public MainWindow(AppConfig config, SettingsStore settingsStore, QuoteService quoteService)
    {
        InitializeComponent();

        _config = config;
        _settingsStore = settingsStore;
        _quoteService = quoteService;
        _refreshTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Max(500, _config.RefreshIntervalMs)));
        _topmostTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2),
            IsEnabled = false
        };
        _topmostTimer.Tick += TopmostTimer_Tick;
        _currentMarket = _config.GetCurrentMarket();

        Left = _config.Window.Left;
        Top = _config.Window.Top;
        Topmost = _config.Window.Topmost;
        BuildContextMenu();
        ApplyMarket(_currentMarket, save: false);
    }

    public MarketDefinition CurrentMarket => _currentMarket;

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Topmost = _config.Window.Topmost;
        ForceTopmost();
        Activate();
    }

    public void ExitApplication()
    {
        _allowClose = true;
        _topmostTimer.Stop();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    public void SwitchMarket(MarketDefinition market)
    {
        ApplyMarket(market, save: true);
        _ = RefreshOnceAsync();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        HideFromAltTab();
        ForceTopmost();
        _topmostTimer.Start();
        await RefreshOnceAsync();
        _ = RunRefreshLoopAsync(_shutdown.Token);
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        ForceTopmost();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        SaveWindowPosition();

        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _shutdown.Cancel();
        _refreshTimer.Dispose();
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _topmostTimer.Stop();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
            SaveWindowPosition();
        }
    }

    private void TopmostTimer_Tick(object? sender, EventArgs e)
    {
        ForceTopmost();
    }

    private void HideFromAltTab()
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        var ex = NativeMethods.GetWindowLongPtr(_handle, -20);
        var newEx = new IntPtr(ex.ToInt64() | (long)NativeMethods.WS_EX_TOOLWINDOW);
        NativeMethods.SetWindowLongPtr(_handle, -20, newEx);
    }

    private void ForceTopmost()
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            _handle,
            NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    private void BuildContextMenu()
    {
        var menu = new ContextMenu();

        foreach (var market in _config.Markets)
        {
            var item = new MenuItem
            {
                Header = market.Name,
                Tag = market,
                IsCheckable = true
            };
            item.Click += (_, _) => SwitchMarket(market);
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());

        var hideItem = new MenuItem { Header = "隐藏" };
        hideItem.Click += (_, _) => Hide();
        menu.Items.Add(hideItem);

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(exitItem);

        ContextMenu = menu;
        RootBorder.ContextMenu = menu;
    }

    private void ApplyMarket(MarketDefinition market, bool save)
    {
        _currentMarket = market;
        _config.CurrentSymbol = market.Symbol;

        PriceText.Text = "--";
        PriceText.Foreground = new SolidColorBrush(market.ToBrushColor());
        UpdateCheckedMarket();
        UpdateToolTip("--");

        if (save)
        {
            _settingsStore.Save(_config);
        }

        MarketChanged?.Invoke(this, market);
    }

    private void UpdateCheckedMarket()
    {
        if (ContextMenu is null)
        {
            return;
        }

        foreach (var item in ContextMenu.Items.OfType<MenuItem>().Where(item => item.Tag is MarketDefinition))
        {
            item.IsChecked = ReferenceEquals(item.Tag, _currentMarket);
        }
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _refreshTimer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshOnceAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshOnceAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);

        try
        {
            var quote = await _quoteService.GetLatestAsync(_currentMarket, _refreshCts.Token);
            Dispatcher.Invoke(() =>
            {
                PriceText.Text = quote.Price;
                UpdateToolTip(quote.Price);
                PriceChanged?.Invoke(this, $"{quote.Name} {quote.Price}");
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            Dispatcher.Invoke(() =>
            {
                PriceText.Text = "ERR";
                UpdateToolTip("获取失败");
                PriceChanged?.Invoke(this, $"{_currentMarket.Name} 获取失败");
            });
        }
    }

    private void UpdateToolTip(string value)
    {
        ToolTip = $"{_currentMarket.Name} {value}";
    }

    private void SaveWindowPosition()
    {
        if (double.IsFinite(Left) && double.IsFinite(Top))
        {
            _config.Window.Left = Left;
            _config.Window.Top = Top;
            _config.Window.Topmost = Topmost;
            _settingsStore.Save(_config);
        }
    }
}
