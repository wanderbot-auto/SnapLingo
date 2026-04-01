using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace SnapLingoWindows;

public sealed class TranslationPanelWindow : Window
{
    private const int PanelWidth = 520;
    private const int MinPanelHeight = 320;
    private const int MaxPanelHeight = 620;

    private readonly MainViewModel viewModel;
    private readonly TranslationPanelPage page;
    private readonly AppWindow appWindow;
    private readonly nint hwnd;
    private bool isClosed;

    public bool IsClosed => isClosed;

    public TranslationPanelWindow(MainViewModel viewModel)
    {
        this.viewModel = viewModel;
        page = new TranslationPanelPage(viewModel, UpdateContentHeight);
        Content = page;
        Title = viewModel.Localizer.Get("window_panel_title");
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(page.TitleBarElement);

        hwnd = WindowNative.GetWindowHandle(this);
        appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));

        ConfigureWindow();
        viewModel.HideRequested += OnHideRequested;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += OnClosed;
    }

    public void PresentPanel()
    {
        UpdateContentHeight(page.MeasurePreferredHeight());
        Activate();
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNORMAL);
        TryPositionNearCursor();
        NativeMethods.SetForegroundWindow(hwnd);
    }

    public void HidePanel()
    {
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
    }

    private void ConfigureWindow()
    {
        appWindow.Resize(new Windows.Graphics.SizeInt32(PanelWidth, 340));
        NativeWindowStyler.ApplySettingsShellStyle(hwnd);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
        }
    }

    private void UpdateContentHeight(double desiredHeight)
    {
        if (double.IsNaN(desiredHeight) || double.IsInfinity(desiredHeight))
        {
            return;
        }

        try
        {
            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            var maxHeight = Math.Min(MaxPanelHeight, Math.Max(MinPanelHeight, displayArea.WorkArea.Height - 40));
            var nextHeight = (int)Math.Clamp(Math.Ceiling(desiredHeight), MinPanelHeight, maxHeight);

            if (appWindow.Size.Width != PanelWidth || appWindow.Size.Height != nextHeight)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32(PanelWidth, nextHeight));
                EnsureWindowStaysVisible(displayArea.WorkArea);
            }
        }
        catch (COMException)
        {
            // Keep the panel usable even if content-driven sizing races initialization.
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
            MoveIntoWorkArea(workArea, desiredX, desiredY);
        }
        catch (COMException)
        {
            // First activation can race window initialization; keep the panel open even if positioning is skipped.
        }
    }

    private void EnsureWindowStaysVisible(Windows.Graphics.RectInt32 workArea)
    {
        var currentPosition = appWindow.Position;
        MoveIntoWorkArea(workArea, currentPosition.X, currentPosition.Y);
    }

    private void MoveIntoWorkArea(Windows.Graphics.RectInt32 workArea, int desiredX, int desiredY)
    {
        var minX = workArea.X + 20;
        var minY = workArea.Y + 20;
        var maxX = workArea.X + workArea.Width - appWindow.Size.Width - 20;
        var maxY = workArea.Y + workArea.Height - appWindow.Size.Height - 20;

        var targetX = Math.Clamp(desiredX, minX, maxX);
        var targetY = Math.Clamp(desiredY, minY, maxY);

        appWindow.Move(new Windows.Graphics.PointInt32(targetX, targetY));
    }

    private void OnHideRequested(object? sender, EventArgs e)
    {
        HidePanel();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        isClosed = true;
        viewModel.HideRequested -= OnHideRequested;
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Closed -= OnClosed;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedLanguage))
        {
            Title = viewModel.Localizer.Get("window_panel_title");
        }
    }
}
