using System.Runtime.InteropServices;

namespace SnapLingoWindows.Services;

public static class NativeWindowStyler
{
    public static void ApplySettingsShellStyle(nint hwnd)
    {
        UpdateStyle(
            hwnd,
            NativeMethods.GWL_STYLE,
            style => style
                & ~NativeMethods.WS_CAPTION
                & ~NativeMethods.WS_THICKFRAME
                & ~NativeMethods.WS_MAXIMIZEBOX);

        UpdateStyle(
            hwnd,
            NativeMethods.GWL_EXSTYLE,
            style => style & ~NativeMethods.WS_EX_CLIENTEDGE & ~NativeMethods.WS_EX_STATICEDGE);

        if (!NativeMethods.SetWindowPos(
                hwnd,
                0,
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE
                | NativeMethods.SWP_NOSIZE
                | NativeMethods.SWP_NOZORDER
                | NativeMethods.SWP_NOACTIVATE
                | NativeMethods.SWP_FRAMECHANGED))
        {
            throw new InvalidOperationException(
                $"Failed to refresh window chrome. Win32 error: {Marshal.GetLastWin32Error()}");
        }

        TryApplyRoundedCorners(hwnd);
        TryRemoveDwmBorder(hwnd);
    }

    public static void ApplyOverlayToolStyle(nint hwnd)
    {
        UpdateStyle(
            hwnd,
            NativeMethods.GWL_STYLE,
            style => style
                & ~NativeMethods.WS_CAPTION
                & ~NativeMethods.WS_THICKFRAME
                & ~NativeMethods.WS_MAXIMIZEBOX
                & ~NativeMethods.WS_SYSMENU);

        UpdateStyle(
            hwnd,
            NativeMethods.GWL_EXSTYLE,
            style => (style
                & ~NativeMethods.WS_EX_CLIENTEDGE
                & ~NativeMethods.WS_EX_STATICEDGE)
                | NativeMethods.WS_EX_TOOLWINDOW);

        if (!NativeMethods.SetWindowPos(
                hwnd,
                0,
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE
                | NativeMethods.SWP_NOSIZE
                | NativeMethods.SWP_NOZORDER
                | NativeMethods.SWP_NOACTIVATE
                | NativeMethods.SWP_FRAMECHANGED))
        {
            throw new InvalidOperationException(
                $"Failed to refresh overlay chrome. Win32 error: {Marshal.GetLastWin32Error()}");
        }

        TryApplyRoundedCorners(hwnd);
        TryRemoveDwmBorder(hwnd);
    }

    private static void UpdateStyle(nint hwnd, int index, Func<uint, uint> transform)
    {
        Marshal.SetLastPInvokeError(0);
        var currentStylePtr = NativeMethods.GetWindowLongPtr(hwnd, index);
        var readError = Marshal.GetLastWin32Error();

        if (currentStylePtr == 0 && readError != 0)
        {
            throw new InvalidOperationException(
                $"Failed to read window style index {index}. Win32 error: {readError}");
        }

        var currentStyle = unchecked((uint)currentStylePtr.ToInt64());
        var updatedStyle = transform(currentStyle);

        if (updatedStyle == currentStyle)
        {
            return;
        }

        Marshal.SetLastPInvokeError(0);
        var previousStyle = NativeMethods.SetWindowLongPtr(hwnd, index, new nint(unchecked((long)updatedStyle)));
        var writeError = Marshal.GetLastWin32Error();

        if (previousStyle == 0 && writeError != 0)
        {
            throw new InvalidOperationException(
                $"Failed to update window style index {index}. Win32 error: {writeError}");
        }
    }

    private static void TryRemoveDwmBorder(nint hwnd)
    {
        var color = NativeMethods.DWM_COLOR_NONE;
        _ = NativeMethods.DwmSetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_BORDER_COLOR,
            in color,
            sizeof(uint));
    }

    private static void TryApplyRoundedCorners(nint hwnd)
    {
        var preference = NativeMethods.DWM_WINDOW_CORNER_PREFERENCE_ROUND;
        _ = NativeMethods.DwmSetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
            in preference,
            sizeof(uint));
    }
}
