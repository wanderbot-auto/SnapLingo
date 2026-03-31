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
        Content = new MainPage(viewModel);
        Title = "SnapLingo Settings";
        SystemBackdrop = new MicaBackdrop();

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
        appWindow.Resize(new Windows.Graphics.SizeInt32(580, 620));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
        }
    }

    private void OnGlobalHotkeyPressed()
    {
        (Application.Current as App)?.ShowTranslationPanel();
        _ = viewModel.HandleHotkeyAsync();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        hotkeyService.Dispose();
        Closed -= OnClosed;
    }
}
