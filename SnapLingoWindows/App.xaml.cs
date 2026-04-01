using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace SnapLingoWindows;

public partial class App : Application
{
    private AutoSelectionMonitorService? autoSelectionMonitor;

    public MainViewModel ViewModel { get; private set; } = null!;
    public MainWindow SettingsWindow { get; private set; } = null!;
    public TranslationPanelWindow? TranslationWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ViewModel = new MainViewModel();
        SettingsWindow = new MainWindow(ViewModel);
        SettingsWindow.Activate();
        autoSelectionMonitor = new AutoSelectionMonitorService(
            DispatcherQueue.GetForCurrentThread()!,
            new ClipboardSelectionCaptureService(),
            OnAutoSelectionDetectedAsync);
        autoSelectionMonitor.Start();
    }

    public void ShowTranslationPanel()
    {
        EnsureTranslationWindow();

        try
        {
            TranslationWindow!.PresentPanel();
        }
        catch (COMException)
        {
            TranslationWindow = CreateTranslationWindow();
            TranslationWindow.PresentPanel();
        }
    }

    public void HideSettingsWindow()
    {
        SettingsWindow.HideWindow();
    }

    public void RestoreSettingsWindow()
    {
        SettingsWindow.ShowWindow();
    }

    public void HideTranslationPanel()
    {
        TranslationWindow?.HidePanel();
    }

    public void ExitApplication()
    {
        autoSelectionMonitor?.Dispose();
        autoSelectionMonitor = null;
        Exit();
    }

    private async Task OnAutoSelectionDetectedAsync(string text)
    {
        ShowTranslationPanel();
        await ViewModel.HandleDetectedSelectionAsync(text);
    }

    private void EnsureTranslationWindow()
    {
        if (TranslationWindow is null || TranslationWindow.IsClosed)
        {
            TranslationWindow = CreateTranslationWindow();
        }
    }

    private TranslationPanelWindow CreateTranslationWindow()
    {
        var window = new TranslationPanelWindow(ViewModel);
        window.Closed += OnTranslationWindowClosed;
        return window;
    }

    private void OnTranslationWindowClosed(object sender, WindowEventArgs args)
    {
        if (sender is not TranslationPanelWindow window)
        {
            return;
        }

        window.Closed -= OnTranslationWindowClosed;

        if (ReferenceEquals(TranslationWindow, window))
        {
            TranslationWindow = null;
        }
    }
}
