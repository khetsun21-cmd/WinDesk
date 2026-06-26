using System.Drawing;
using MarketTicker.Interop;
using MarketTicker.Models;
using Forms = System.Windows.Forms;

namespace MarketTicker.Services;

public sealed class TrayIconController : IDisposable
{
    private readonly MainWindow _window;
    private readonly InputLockService _lockService;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly List<Forms.ToolStripMenuItem> _marketItems = [];
    private Forms.ToolStripMenuItem? _lockItem;
    private bool _lockMenuUpdating;

    public TrayIconController(MainWindow window, IReadOnlyCollection<MarketDefinition> markets,
        InputLockService lockService)
    {
        _window = window;
        _lockService = lockService;
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = BuildIcon("GC"),
            Text = "MarketTicker",
            Visible = true,
            ContextMenuStrip = BuildMenu(markets)
        };

        _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
        _window.MarketChanged += Window_MarketChanged;
        _window.PriceChanged += Window_PriceChanged;
        _lockService.LockStateChanged += LockService_LockStateChanged;
        UpdateMarket(_window.CurrentMarket);
    }

    private Forms.ContextMenuStrip BuildMenu(IReadOnlyCollection<MarketDefinition> markets)
    {
        var menu = new Forms.ContextMenuStrip();

        // Lock / unlock
        _lockItem = new Forms.ToolStripMenuItem("锁定 Ctrl+;");
        _lockItem.Click += (_, _) => _lockService.ToggleLock();
        menu.Items.Add(_lockItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        foreach (var market in markets)
        {
            var item = new Forms.ToolStripMenuItem(market.Name)
            {
                Tag = market,
                CheckOnClick = false
            };
            item.Click += (_, _) => _window.SwitchMarket(market);
            _marketItems.Add(item);
            menu.Items.Add(item);
        }

        menu.Items.Add(new Forms.ToolStripSeparator());

        var showItem = new Forms.ToolStripMenuItem("显示");
        showItem.Click += (_, _) => _window.ShowFromTray();
        menu.Items.Add(showItem);

        var hideItem = new Forms.ToolStripMenuItem("隐藏");
        hideItem.Click += (_, _) => _window.Hide();
        menu.Items.Add(hideItem);

        var exitItem = new Forms.ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => _window.ExitApplication();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void LockService_LockStateChanged(object? sender, bool locked)
    {
        if (_lockItem is null) return;
        _lockMenuUpdating = true;
        _lockItem.Text = locked ? "已锁定 (Ctrl+; 解锁)" : "锁定 Ctrl+;";
        _lockItem.Checked = locked;
        _lockMenuUpdating = false;
        UpdateText(locked ? "🔒 LOCKED" : _notifyIcon.Text.Replace("🔒 LOCKED", "").Trim());
    }

    private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
    {
        _window.ShowFromTray();
    }

    private void Window_MarketChanged(object? sender, MarketDefinition market)
    {
        UpdateMarket(market);
    }

    private void Window_PriceChanged(object? sender, string text)
    {
        UpdateText(text);
    }

    private void UpdateMarket(MarketDefinition market)
    {
        foreach (var item in _marketItems)
        {
            item.Checked = ReferenceEquals(item.Tag, market);
        }

        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Icon = BuildIcon(market.DisplayCode);
        UpdateText($"{market.Name} --");
    }

    private void UpdateText(string text)
    {
        if (_lockMenuUpdating) return;
        var prefix = _lockService.IsLocked ? "🔒 " : "";
        var full = prefix + text;
        _notifyIcon.Text = full.Length > 63 ? full[..63] : full;
    }

    private static Icon BuildIcon(string label)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(17, 20, 23));
        using var brush = new SolidBrush(Color.White);
        using var font = new Font("Segoe UI", label.Length <= 2 ? 12 : 9, FontStyle.Bold, GraphicsUnit.Pixel);
        var shortLabel = string.IsNullOrWhiteSpace(label) ? "M" : label.Trim();
        if (shortLabel.Length > 4)
        {
            shortLabel = shortLabel[..4];
        }

        var size = graphics.MeasureString(shortLabel, font);
        graphics.DrawString(shortLabel, font, brush, (32 - size.Width) / 2, (32 - size.Height) / 2);

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    public void Dispose()
    {
        _lockService.LockStateChanged -= LockService_LockStateChanged;
        _notifyIcon.DoubleClick -= NotifyIcon_DoubleClick;
        _window.MarketChanged -= Window_MarketChanged;
        _window.PriceChanged -= Window_PriceChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();
    }
}
