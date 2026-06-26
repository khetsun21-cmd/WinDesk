using System.Runtime.InteropServices;
using System.Text;
using MarketTicker.Interop;
using Forms = System.Windows.Forms;

namespace MarketTicker.Services;

public sealed class InputLockService : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _keyboardProc;
    private readonly NativeMethods.LowLevelMouseProc _mouseProc;
    private readonly string _password;
    private readonly StringBuilder _typedBuffer = new(64);
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private bool _isLocked;
    private bool _isSessionLocked;
    private MessageWindow? _msgWindow;

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
        LockStateChanged?.Invoke(this, true);
    }

    public void Unlock()
    {
        if (!_isLocked) return;
        _isLocked = false;
        _typedBuffer.Clear();
        RemoveHooks();
        LockStateChanged?.Invoke(this, false);
    }

    internal void OnSessionChange(int sessionEvent)
    {
        if (sessionEvent == NativeMethods.WTS_SESSION_LOCK)
        {
            _isSessionLocked = true;
            RemoveHooks();
        }
        else if (sessionEvent == NativeMethods.WTS_SESSION_UNLOCK)
        {
            _isSessionLocked = false;
            if (_isLocked) InstallHooks();
        }
    }

    private void InstallHooks()
    {
        try
        {
            var hMod = NativeMethods.GetModuleHandle(null);
            if (_keyboardHook == IntPtr.Zero)
                _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
            if (_mouseHook == IntPtr.Zero)
                _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, hMod, 0);
        }
        catch { /* admin rights may be required */ }
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

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        if (_isSessionLocked || !_isLocked)
            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        // On key-down, accumulate characters and check password
        if (wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN)
        {
            var vkCode = (uint)Marshal.ReadInt32(lParam);
            var ch = VkToChar(vkCode);
            if (ch.HasValue)
            {
                _typedBuffer.Append(ch.Value);
                // keep buffer bounded to 3x password length
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

        // suppress all keyboard input when locked
        return (IntPtr)1;
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        if (_isSessionLocked || !_isLocked)
            return NativeMethods.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        // suppress all mouse input when locked
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
        _msgWindow?.Dispose();
        _msgWindow = null;
    }

    // -------------------------------------------------------
    // Hidden native window for WM_HOTKEY + WM_WTSSESSION_CHANGE
    // -------------------------------------------------------
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
