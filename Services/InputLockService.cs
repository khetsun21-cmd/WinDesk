using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MarketTicker.Interop;
using Forms = System.Windows.Forms;

namespace MarketTicker.Services;

public sealed class InputLockService : IDisposable
{
    private static readonly string[] BlockedWindows =
        ["taskmgr", "cmd", "powershell", "pwsh", "regedit", "mmc", "msconfig"];

    private readonly NativeMethods.LowLevelKeyboardProc _keyboardProc;
    private readonly NativeMethods.LowLevelMouseProc _mouseProc;
    private readonly string _password;
    private readonly StringBuilder _typedBuffer = new(64);
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private bool _isLocked;
    private bool _isSessionLocked;
    private MessageWindow? _msgWindow;
    private CancellationTokenSource? _windowMonitorCts;

    public bool IsLocked => _isLocked;

    public event EventHandler<bool>? LockStateChanged;

    public InputLockService(string password)
    {
        _password = password ?? string.Empty;
        _keyboardProc = KeyboardHookProc;
        _mouseProc = MouseHookProc;
        _msgWindow = new MessageWindow(this);
    }

    public void ToggleLock()
    {
        if (_isLocked) Unlock();
        else Lock();
    }

    public void Lock()
    {
        if (_isLocked || _isSessionLocked || string.IsNullOrEmpty(_password)) return;
        _isLocked = true;
        _typedBuffer.Clear();
        InstallHooks();
        StartWindowMonitor();
        LockStateChanged?.Invoke(this, true);
    }

    public void Unlock()
    {
        if (!_isLocked) return;
        _isLocked = false;
        _typedBuffer.Clear();
        RemoveHooks();
        StopWindowMonitor();
        LockStateChanged?.Invoke(this, false);
    }

    internal void OnSessionChange(int sessionEvent)
    {
        if (sessionEvent == NativeMethods.WTS_SESSION_LOCK)
        {
            _isSessionLocked = true;
            RemoveHooks();
            StopWindowMonitor();
        }
        else if (sessionEvent == NativeMethods.WTS_SESSION_UNLOCK)
        {
            _isSessionLocked = false;
            if (_isLocked)
            {
                InstallHooks();
                StartWindowMonitor();
            }
        }
    }

    // ---- Window monitor (WinClose, not process kill) ----
    private void StartWindowMonitor()
    {
        _windowMonitorCts?.Cancel();
        _windowMonitorCts?.Dispose();
        _windowMonitorCts = new CancellationTokenSource();
        var token = _windowMonitorCts.Token;
        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(300, token);
                CloseBlockedWindows();
            }
        }, token);
    }

    private void StopWindowMonitor()
    {
        _windowMonitorCts?.Cancel();
        _windowMonitorCts?.Dispose();
        _windowMonitorCts = null;
    }

    private static void CloseBlockedWindows()
    {
        foreach (var name in BlockedWindows)
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName(name))
                {
                    if (!proc.HasExited)
                    {
                        try { proc.CloseMainWindow(); } catch { }
                        proc.WaitForExit(200);
                        if (!proc.HasExited)
                            try { proc.Kill(); } catch { }
                    }
                    proc.Dispose();
                }
            }
            catch { }
        }
    }

    // ---- Hook management ----
    private void InstallHooks()
    {
        try
        {
            var hMod = NativeMethods.GetModuleHandle(null);
            if (_keyboardHook == IntPtr.Zero)
                _keyboardHook = NativeMethods.SetWindowsHookEx(
                    NativeMethods.WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
            if (_mouseHook == IntPtr.Zero)
                _mouseHook = NativeMethods.SetWindowsHookEx(
                    NativeMethods.WH_MOUSE_LL, _mouseProc, hMod, 0);
        }
        catch { }
    }

    private void RemoveHooks()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
    }

    // ---- Hook callbacks ----
    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        if (_isSessionLocked || !_isLocked)
            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        if (wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN)
        {
            var vkCode = (uint)Marshal.ReadInt32(lParam);
            var ch = VkToChar(vkCode);
            if (ch.HasValue)
            {
                _typedBuffer.Append(ch.Value);
                var maxLen = Math.Max(_password.Length * 3, 8);
                if (_typedBuffer.Length > maxLen)
                    _typedBuffer.Remove(0, _typedBuffer.Length - maxLen);

                if (_typedBuffer.Length >= _password.Length &&
                    _typedBuffer.ToString().EndsWith(_password, StringComparison.Ordinal))
                {
                    Unlock();
                }
            }
        }

        return (IntPtr)1;
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        if (_isSessionLocked || !_isLocked)
            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        return (IntPtr)1;
    }

    private static char? VkToChar(uint vkCode)
    {
        var shift = (NativeMethods.GetKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
        var caps = (NativeMethods.GetKeyState(NativeMethods.VK_CAPITAL) & 1) != 0;
        var upper = shift ^ caps;

        return vkCode switch
        {
            >= 0x41 and <= 0x5A => upper ? (char)vkCode : (char)(vkCode + 32),
            >= 0x30 and <= 0x39 when !shift => (char)vkCode,
            >= 0x60 and <= 0x69 => (char)('0' + (vkCode - 0x60)),
            0x20 => ' ',
            _ => null
        };
    }

    public void Dispose()
    {
        RemoveHooks();
        StopWindowMonitor();
        _msgWindow?.Dispose();
        _msgWindow = null;
    }

    private sealed class MessageWindow : Forms.NativeWindow, IDisposable
    {
        private readonly InputLockService _service;
        private const int HotkeyId = 0x42;

        public MessageWindow(InputLockService service)
        {
            _service = service;
            CreateHandle(new Forms.CreateParams { Caption = "MtLockMsg" });
            NativeMethods.RegisterHotKey(Handle, HotkeyId,
                NativeMethods.MOD_CONTROL | NativeMethods.MOD_NOREPEAT, NativeMethods.VK_OEM_1);
            NativeMethods.WTSRegisterSessionNotification(Handle, NativeMethods.NOTIFY_FOR_THIS_SESSION);
        }

        protected override void WndProc(ref Forms.Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam == HotkeyId)
                _service.ToggleLock();
            else if (m.Msg == NativeMethods.WM_WTSSESSION_CHANGE)
                _service.OnSessionChange((int)m.WParam);

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            NativeMethods.WTSUnRegisterSessionNotification(Handle);
            DestroyHandle();
        }
    }
}
