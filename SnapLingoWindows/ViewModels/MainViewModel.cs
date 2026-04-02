using Microsoft.UI.Xaml;
using SnapLingoWindows.Infrastructure;
using SnapLingoWindows.Services;

namespace SnapLingoWindows.ViewModels;

public sealed class MainViewModel : BindableBase
{
    private readonly JsonSettingsStore settingsStore;
    private readonly LocalizationService localizer;
    private readonly ProviderRegistry providerRegistry;
    private readonly AppSettingsDocument settingsDocument;

    public MainViewModel()
    {
        settingsStore = new JsonSettingsStore();
        settingsDocument = settingsStore.Load();

        var selectedProvider = Enum.TryParse(settingsDocument.SelectedProvider, out ProviderKind parsedProvider)
            ? parsedProvider
            : ProviderKind.OpenAI;
        var selectedShortcutPreset = Enum.TryParse(settingsDocument.SelectedShortcutPreset, out ShortcutPreset parsedShortcutPreset)
            ? parsedShortcutPreset
            : ShortcutPresetExtensions.DefaultPreset;
        var selectedLanguage = Enum.TryParse(settingsDocument.SelectedLanguage, out AppLanguage parsedLanguage)
            ? parsedLanguage
            : AppLanguage.English;

        localizer = new LocalizationService(selectedLanguage);
        providerRegistry = new ProviderRegistry(new SecureSecretStore(), localizer)
        {
            SelectedProvider = selectedProvider,
        };

        ProviderSettings = new ProviderSettingsViewModel(localizer, providerRegistry, settingsDocument, SaveSettings, selectedProvider);
        PromptProfiles = new PromptProfilesViewModel(localizer, providerRegistry, settingsDocument, SaveSettings);
        AppearanceSettings = new AppearanceSettingsViewModel(localizer, settingsDocument, SaveSettings, selectedLanguage);
        HotkeySettings = new HotkeySettingsViewModel(settingsDocument, SaveSettings, selectedShortcutPreset);
        WorkflowPanel = new WorkflowPanelViewModel(
            localizer,
            new ClipboardSelectionCaptureService(),
            providerRegistry,
            () => ProviderSettings.SelectedModelId,
            () => AppearanceSettings.SelectedLanguage);

        ProviderSettings.PropertyChanged += OnProviderSettingsChanged;
        PromptProfiles.PropertyChanged += OnPromptProfilesChanged;
        AppearanceSettings.PropertyChanged += OnAppearanceSettingsChanged;
        HotkeySettings.PropertyChanged += OnHotkeySettingsChanged;
        WorkflowPanel.PropertyChanged += OnWorkflowPanelChanged;
        WorkflowPanel.ShowRequested += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
        WorkflowPanel.HideRequested += (_, _) => HideRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ShowRequested;
    public event EventHandler? HideRequested;

    public ProviderSettingsViewModel ProviderSettings { get; }

    public PromptProfilesViewModel PromptProfiles { get; }

    public AppearanceSettingsViewModel AppearanceSettings { get; }

    public HotkeySettingsViewModel HotkeySettings { get; }

    public WorkflowPanelViewModel WorkflowPanel { get; }

    public WorkflowStateStore Workflow => WorkflowPanel.Workflow;

    public LocalizationService Localizer => localizer;

    public AppLanguage SelectedLanguage => AppearanceSettings.SelectedLanguage;

    public ShortcutPreset SelectedShortcutPreset => HotkeySettings.SelectedShortcutPreset;

    public string SelectedModelId => ProviderSettings.SelectedModelId;

    public string ActiveProviderDisplayName => ProviderSettings.SelectedProviderDisplayName;

    public string SelectedShortcutDisplayName => HotkeySettings.SelectedShortcutDisplayName;

    public string StorageStatusText => localizer.Get("storage_secure_local");

    public string WorkflowStatusText => WorkflowPanel.WorkflowStatusText;

    public string CredentialStatusText => ProviderSettings.CredentialStatusText;

    public string SelectedLanguageLabel => AppearanceSettings.SelectedLanguageLabel;

    public string? OverviewStatusMessage => HotkeySettings.HotkeyStatusMessage ?? ProviderSettings.ProviderStatusMessage;

    public Visibility OverviewStatusVisibility =>
        string.IsNullOrWhiteSpace(OverviewStatusMessage) ? Visibility.Collapsed : Visibility.Visible;

    public async Task InitializeAsync()
    {
        await ProviderSettings.InitializeAsync();
    }

    public async Task HandleHotkeyAsync()
    {
        await WorkflowPanel.HandleHotkeyAsync();
    }

    public async Task HandleDetectedSelectionAsync(string text)
    {
        await WorkflowPanel.HandleCapturedSelectionAsync(text);
    }

    public async Task HandleSelectionLauncherAsync(string? text)
    {
        await WorkflowPanel.HandleSelectionLauncherAsync(text);
    }

    public async Task RetryCurrentFlowAsync()
    {
        await WorkflowPanel.RetryCurrentFlowAsync();
    }

    public async Task UseCurrentClipboardAsync()
    {
        await WorkflowPanel.UseCurrentClipboardAsync();
    }

    public async Task SelectModeAsync(TranslationMode mode)
    {
        await WorkflowPanel.SelectModeAsync(mode);
    }

    public async Task CopyPrimaryResultAsync()
    {
        await WorkflowPanel.CopyPrimaryResultAsync();
    }

    public async Task SaveApiKeyAsync()
    {
        await ProviderSettings.SaveApiKeyAsync();
    }

    public void ClearApiKey()
    {
        ProviderSettings.ClearApiKey();
    }

    public void SetHotkeyStatusMessage(string message)
    {
        HotkeySettings.SetHotkeyStatusMessage(message);
    }

    private void SaveSettings()
    {
        settingsStore.Save(settingsDocument);
    }

    private void OnProviderSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        WorkflowPanel.NotifyProviderSelectionChanged();

        switch (e.PropertyName)
        {
            case nameof(ProviderSettingsViewModel.SelectedProviderDisplayName):
                OnPropertyChanged(nameof(ActiveProviderDisplayName));
                break;
            case nameof(ProviderSettingsViewModel.SelectedModelId):
                OnPropertyChanged(nameof(SelectedModelId));
                break;
            case nameof(ProviderSettingsViewModel.CredentialStatusText):
                OnPropertyChanged(nameof(CredentialStatusText));
                break;
            case nameof(ProviderSettingsViewModel.ProviderStatusMessage):
            case nameof(ProviderSettingsViewModel.ProviderStatusVisibility):
                OnPropertyChanged(nameof(OverviewStatusMessage));
                OnPropertyChanged(nameof(OverviewStatusVisibility));
                break;
        }
    }

    private void OnPromptProfilesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PromptProfilesViewModel.PromptStatusMessage))
        {
            OnPropertyChanged(nameof(OverviewStatusMessage));
            OnPropertyChanged(nameof(OverviewStatusVisibility));
        }
    }

    private void OnAppearanceSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppearanceSettingsViewModel.SelectedLanguage) ||
            e.PropertyName == nameof(AppearanceSettingsViewModel.SelectedLanguageChoice))
        {
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(SelectedLanguageLabel));
            OnPropertyChanged(nameof(StorageStatusText));
            OnPropertyChanged(nameof(CredentialStatusText));
            OnPropertyChanged(nameof(WorkflowStatusText));
            WorkflowPanel.NotifyLanguageChanged();
        }
    }

    private void OnHotkeySettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HotkeySettingsViewModel.SelectedShortcutPreset) ||
            e.PropertyName == nameof(HotkeySettingsViewModel.SelectedShortcutChoice))
        {
            OnPropertyChanged(nameof(SelectedShortcutPreset));
            OnPropertyChanged(nameof(SelectedShortcutDisplayName));
        }

        if (e.PropertyName == nameof(HotkeySettingsViewModel.HotkeyStatusMessage) ||
            e.PropertyName == nameof(HotkeySettingsViewModel.HotkeyStatusVisibility))
        {
            OnPropertyChanged(nameof(OverviewStatusMessage));
            OnPropertyChanged(nameof(OverviewStatusVisibility));
        }
    }

    private void OnWorkflowPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkflowPanelViewModel.WorkflowStatusText))
        {
            OnPropertyChanged(nameof(WorkflowStatusText));
        }
    }
}
