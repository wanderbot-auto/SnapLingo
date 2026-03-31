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
        TranslationWindow ??= new TranslationPanelWindow(ViewModel);
        TranslationWindow.PresentPanel();
    }
}
