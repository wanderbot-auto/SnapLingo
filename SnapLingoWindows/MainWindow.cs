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
        Title = viewModel.Localizer.Get("window_settings_title");
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(page.TitleBarElement);

        hwnd = WindowNative.GetWindowHandle(this);
        appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));
        hotkeyService = new GlobalHotkeyService(hwnd, viewModel.Localizer, OnGlobalHotkeyPressed);

        ConfigureWindow();
        ApplyHotkeyPreset(viewModel.SelectedShortcutPreset);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += OnClosed;
    }

    public void ApplyHotkeyPreset(ShortcutPreset preset)
    {
        viewModel.SetHotkeyStatusMessage(hotkeyService.Register(preset));
    }

    private void ConfigureWindow()
    {
        appWindow.Resize(new Windows.Graphics.SizeInt32(1040, 760));
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
        titleBar.ButtonForegroundColor = Color.FromArgb(255, 64, 64, 64);
        titleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 140, 140, 140);
        titleBar.ButtonHoverBackgroundColor = Color.FromArgb(20, 0, 0, 0);
        titleBar.ButtonPressedBackgroundColor = Color.FromArgb(32, 0, 0, 0);
        titleBar.ButtonHoverForegroundColor = Color.FromArgb(255, 23, 23, 23);
        titleBar.ButtonPressedForegroundColor = Color.FromArgb(255, 23, 23, 23);
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        hotkeyService.Dispose();
        Closed -= OnClosed;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedLanguage))
        {
            Title = viewModel.Localizer.Get("window_settings_title");
            ApplyHotkeyPreset(viewModel.SelectedShortcutPreset);
        }
    }
}
