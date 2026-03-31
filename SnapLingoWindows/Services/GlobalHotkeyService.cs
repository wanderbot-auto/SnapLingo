using System.Runtime.InteropServices;

namespace SnapLingoWindows.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x534C;

    private readonly nint hwnd;
    private readonly LocalizationService localizer;
    private readonly Action onHotkeyPressed;
    private readonly WindowProcedure windowProcedure;
    private nint previousWindowProcedure;
    private bool isRegistered;

    public GlobalHotkeyService(nint hwnd, LocalizationService localizer, Action onHotkeyPressed)
    {
        this.hwnd = hwnd;
        this.localizer = localizer;
        this.onHotkeyPressed = onHotkeyPressed;
        windowProcedure = WindowProc;
        previousWindowProcedure = NativeMethods.SetWindowLongPtr(
            hwnd,
            NativeMethods.GWLP_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(windowProcedure)
        );
    }

    public string Register(ShortcutPreset preset)
    {
        Unregister();

        var registration = preset.ToHotkeyRegistration();
        if (!NativeMethods.RegisterHotKey(hwnd, HotkeyId, registration.Modifiers, registration.VirtualKey))
        {
            var errorCode = Marshal.GetLastWin32Error();
            return localizer.Format("error_hotkey_register", preset.DisplayName(), errorCode);
        }

        isRegistered = true;
        return localizer.Format("status_hotkey_set", preset.DisplayName());
    }

    public void Dispose()
    {
        Unregister();

        if (previousWindowProcedure != 0)
        {
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWLP_WNDPROC, previousWindowProcedure);
            previousWindowProcedure = 0;
        }

        GC.SuppressFinalize(this);
    }

    private void Unregister()
    {
        if (!isRegistered)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(hwnd, HotkeyId);
        isRegistered = false;
    }

    private nint WindowProc(nint windowHandle, uint message, nuint wParam, nint lParam)
    {
        if (message == NativeMethods.WM_HOTKEY && (int)wParam == HotkeyId)
        {
            onHotkeyPressed();
            return 0;
        }

        return NativeMethods.CallWindowProc(previousWindowProcedure, windowHandle, message, wParam, lParam);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProcedure(nint windowHandle, uint message, nuint wParam, nint lParam);
}
