using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using Windows.UI;

namespace SnapLingoWindows;

public sealed class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private readonly GlobalHotkeyService hotkeyService;
    private readonly AppWindow appWindow;
    private readonly nint hwnd;

    public MainWindow(MainViewModel viewModel)
    {
        this.viewModel = viewModel;
        var page = new MainPage(viewModel);
        Content = page;
        Title = "SnapLingo Settings";
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(page.TitleBarElement);

        hwnd = WindowNative.GetWindowHandle(this);
        appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));
        hotkeyService = new GlobalHotkeyService(hwnd, OnGlobalHotkeyPressed);

        ConfigureWindow();
        ApplyHotkeyPreset(viewModel.SelectedShortcutPreset);
        Closed += OnClosed;
    }

    public void ApplyHotkeyPreset(ShortcutPreset preset)
    {
        viewModel.SetHotkeyStatusMessage(hotkeyService.Register(preset));
    }

    private void ConfigureWindow()
    {
        appWindow.Resize(new Windows.Graphics.SizeInt32(690, 620));
        ConfigureTitleBar();

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }
    }

    private void OnGlobalHotkeyPressed()
    {
        (Application.Current as App)?.ShowTranslationPanel();
        _ = viewModel.HandleHotkeyAsync();
    }

    private void ConfigureTitleBar()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = appWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        titleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0, 0, 0, 0);
        titleBar.ButtonForegroundColor = Color.FromArgb(255, 242, 242, 242);
        titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 160, 160, 160);
        titleBar.ButtonHoverBackgroundColor = Color.FromArgb(24, 255, 255, 255);
        titleBar.ButtonPressedBackgroundColor = Color.FromArgb(36, 255, 255, 255);
        titleBar.ButtonHoverForegroundColor = Color.FromArgb(255, 255, 255, 255);
        titleBar.ButtonPressedForegroundColor = Color.FromArgb(255, 255, 255, 255);
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        hotkeyService.Dispose();
        Closed -= OnClosed;
    }
}
