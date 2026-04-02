using System.Runtime.InteropServices;

namespace SnapLingoWindows.Services;

public static partial class NativeMethods
{
    public const uint WM_HOTKEY = 0x0312;
    public const int GWLP_WNDPROC = -4;
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_NOREPEAT = 0x4000;
    public const int VK_LBUTTON = 0x01;

    public const int SW_HIDE = 0;
    public const int SW_SHOWNORMAL = 1;
    public const int SW_SHOWNOACTIVATE = 4;

    public const uint WS_BORDER = 0x00800000;
    public const uint WS_DLGFRAME = 0x00400000;
    public const uint WS_CAPTION = WS_BORDER | WS_DLGFRAME;
    public const uint WS_SYSMENU = 0x00080000;
    public const uint WS_MAXIMIZEBOX = 0x00010000;
    public const uint WS_THICKFRAME = 0x00040000;

    public const uint WS_EX_CLIENTEDGE = 0x00000200;
    public const uint WS_EX_LAYERED = 0x00080000;
    public const uint WS_EX_STATICEDGE = 0x00020000;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint LWA_COLORKEY = 0x00000001;
    public const int RGN_OR = 2;

    public static readonly nint HWND_TOPMOST = new(-1);

    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_BORDER_COLOR = 34;
    public const uint DWM_WINDOW_CORNER_PREFERENCE_DONOTROUND = 1;
    public const uint DWM_WINDOW_CORNER_PREFERENCE_ROUND = 2;
    public const uint DWM_COLOR_NONE = 0xFFFFFFFE;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(nint hWnd, int id);

    [LibraryImport("user32.dll")]
    public static partial uint GetClipboardSequenceNumber();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT point);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint hWnd, int command);

    [LibraryImport("user32.dll")]
    public static partial short GetAsyncKeyState(int virtualKey);

    [LibraryImport("user32.dll")]
    public static partial uint GetDoubleClickTime();

    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
    public static partial nint CallWindowProc(nint previousWindowProc, nint hWnd, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial nint GetWindowLongPtr64(nint hWnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static partial int GetWindowLong32(nint hWnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtr64(nint hWnd, int index, nint newWindowProc);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static partial int SetWindowLong32(nint hWnd, int index, int newWindowProc);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    public static partial nint CreateRectRgn(int left, int top, int right, int bottom);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    public static partial nint CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    public static partial int CombineRgn(nint destinationRegion, nint sourceRegion1, nint sourceRegion2, int combineMode);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint objectHandle);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial int SetWindowRgn(nint hWnd, nint regionHandle, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetLayeredWindowAttributes(
        nint hWnd,
        uint colorKey,
        byte alpha,
        uint flags);

    [LibraryImport("dwmapi.dll", SetLastError = true)]
    public static partial int DwmSetWindowAttribute(
        nint hwnd,
        int attribute,
        in uint value,
        uint attributeSize);

    public static nint GetWindowLongPtr(nint hWnd, int index)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, index)
            : new nint(GetWindowLong32(hWnd, index));
    }

    public static nint SetWindowLongPtr(nint hWnd, int index, nint newWindowProc)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, index, newWindowProc)
            : new nint(SetWindowLong32(hWnd, index, newWindowProc.ToInt32()));
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }
}
