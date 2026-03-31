using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace SnapLingoWindows;

public sealed class TranslationPanelWindow : Window
{
    private readonly MainViewModel viewModel;
    private readonly AppWindow appWindow;
    private readonly nint hwnd;
    private bool isClosed;

    public bool IsClosed => isClosed;

    public TranslationPanelWindow(MainViewModel viewModel)
    {
        this.viewModel = viewModel;
        Content = new TranslationPanelPage(viewModel);
        Title = "SnapLingo Panel";
        SystemBackdrop = new MicaBackdrop();

        hwnd = WindowNative.GetWindowHandle(this);
        appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));

        ConfigureWindow();
        viewModel.HideRequested += OnHideRequested;
        Closed += OnClosed;
    }

    public void PresentPanel()
    {
        Activate();
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNORMAL);
        TryPositionNearCursor();
        NativeMethods.SetForegroundWindow(hwnd);
    }

    private void ConfigureWindow()
    {
        appWindow.Resize(new Windows.Graphics.SizeInt32(520, 520));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }
    }

    private void TryPositionNearCursor()
    {
        if (!NativeMethods.GetCursorPos(out var point))
        {
            return;
        }

        try
        {
            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;

            var desiredX = point.X - (appWindow.Size.Width / 2);
            var desiredY = point.Y - (appWindow.Size.Height / 2);

            var minX = workArea.X + 20;
            var minY = workArea.Y + 20;
            var maxX = workArea.X + workArea.Width - appWindow.Size.Width - 20;
            var maxY = workArea.Y + workArea.Height - appWindow.Size.Height - 20;

            var targetX = Math.Clamp(desiredX, minX, maxX);
            var targetY = Math.Clamp(desiredY, minY, maxY);

            appWindow.Move(new Windows.Graphics.PointInt32(targetX, targetY));
        }
        catch (COMException)
        {
            // First activation can race window initialization; keep the panel open even if positioning is skipped.
        }
    }

    private void OnHideRequested(object? sender, EventArgs e)
    {
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        isClosed = true;
        viewModel.HideRequested -= OnHideRequested;
        Closed -= OnClosed;
    }
}
