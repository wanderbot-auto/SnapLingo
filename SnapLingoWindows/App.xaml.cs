using System.Runtime.InteropServices;

namespace SnapLingoWindows;

public partial class App : Application
{
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
