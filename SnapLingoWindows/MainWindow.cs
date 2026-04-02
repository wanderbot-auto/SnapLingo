using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

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

    public void HideWindow()
    {
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Minimize();
            return;
        }

        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
    }

    public void ShowWindow()
    {
        Activate();
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNORMAL);
        NativeMethods.SetForegroundWindow(hwnd);
    }

    private void ConfigureWindow()
    {
        appWindow.Resize(new Windows.Graphics.SizeInt32(680, 580));
        NativeWindowStyler.ApplySettingsShellStyle(hwnd);
    }

    private void OnGlobalHotkeyPressed()
    {
        _ = viewModel.HandleHotkeyAsync();
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
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.SelectedShortcutPreset))
        {
            ApplyHotkeyPreset(viewModel.SelectedShortcutPreset);
        }
    }
}
