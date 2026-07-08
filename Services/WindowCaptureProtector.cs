using WinDesk.Interop;

namespace WinDesk.Services;

public sealed class WindowCaptureProtector
{
    public const uint WdaExcludeFromCapture = 0x00000011;

    private readonly Func<IntPtr, uint, bool> _setWindowDisplayAffinity;

    public WindowCaptureProtector()
        : this(NativeMethods.SetWindowDisplayAffinity)
    {
    }

    public WindowCaptureProtector(Func<IntPtr, uint, bool> setWindowDisplayAffinity)
    {
        _setWindowDisplayAffinity = setWindowDisplayAffinity;
    }

    public bool ApplyExcludeFromCapture(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        return _setWindowDisplayAffinity(windowHandle, WdaExcludeFromCapture);
    }
}
