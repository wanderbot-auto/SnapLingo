using SnapLingoWindows.Infrastructure;
using SnapLingoWindows.Services;
using Windows.ApplicationModel.DataTransfer;

namespace SnapLingoWindows.ViewModels;

public sealed class MainViewModel : BindableBase
{
    private readonly JsonSettingsStore settingsStore;
    private readonly ProviderRegistry providerRegistry;
    private readonly WorkflowOrchestrator orchestrator;
    private readonly AppSettingsDocument settingsDocument;

    private ProviderKind selectedProvider;
    private ShortcutPreset selectedShortcutPreset;
    private string apiKeyInput = string.Empty;
    private string? providerStatusMessage;
    private string? hotkeyStatusMessage;

    public MainViewModel()
    {
        settingsStore = new JsonSettingsStore();
        settingsDocument = settingsStore.Load();

        if (!Enum.TryParse(settingsDocument.SelectedProvider, out selectedProvider))
        {
            selectedProvider = ProviderKind.OpenAI;
        }

        if (!Enum.TryParse(settingsDocument.SelectedShortcutPreset, out selectedShortcutPreset))
        {
            selectedShortcutPreset = ShortcutPresetExtensions.DefaultPreset;
        }

        var secretStore = new SecureSecretStore();
        providerRegistry = new ProviderRegistry(secretStore)
        {
            SelectedProvider = selectedProvider,
        };
        Workflow = new WorkflowStateStore();
        Workflow.ResetForIdle();
        orchestrator = new WorkflowOrchestrator(Workflow, new ClipboardSelectionCaptureService(), providerRegistry);
        apiKeyInput = providerRegistry.LoadKey(selectedProvider);
    }

    public event EventHandler? HideRequested;

    public WorkflowStateStore Workflow { get; }

    public ProviderKind SelectedProvider
    {
        get => selectedProvider;
        private set => SetProperty(ref selectedProvider, value);
    }

    public ShortcutPreset SelectedShortcutPreset
    {
        get => selectedShortcutPreset;
        private set => SetProperty(ref selectedShortcutPreset, value);
    }

    public string ApiKeyInput
    {
        get => apiKeyInput;
        private set => SetProperty(ref apiKeyInput, value);
    }

    public string? ProviderStatusMessage
    {
        get => providerStatusMessage;
        private set => SetProperty(ref providerStatusMessage, value);
    }

    public string? HotkeyStatusMessage
    {
        get => hotkeyStatusMessage;
        private set => SetProperty(ref hotkeyStatusMessage, value);
    }

    public async Task HandleHotkeyAsync()
    {
        await orchestrator.HandleHotkeyAsync();
    }

    public async Task RetryCurrentFlowAsync()
    {
        await orchestrator.RetryAsync();
    }

    public async Task UseCurrentClipboardAsync()
    {
        await orchestrator.UseCurrentClipboardAsync();
    }

    public async Task SelectModeAsync(TranslationMode mode)
    {
        await orchestrator.SwitchModeAsync(mode);
    }

    public async Task CopyPrimaryResultAsync()
    {
        if (!Workflow.CanCopy || string.IsNullOrWhiteSpace(Workflow.PrimaryText))
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(Workflow.PrimaryText);
        Clipboard.SetContent(package);
        Workflow.ShowCopiedFeedback();

        await Task.Delay(TimeSpan.FromMilliseconds(350));
        Workflow.ResetForIdle();
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateProvider(ProviderKind provider)
    {
        SelectedProvider = provider;
        providerRegistry.SelectedProvider = provider;
        ApiKeyInput = providerRegistry.LoadKey(provider);
        ProviderStatusMessage = $"Switched to {provider.DisplayName()}.";
        SaveSettings();
    }

    public void UpdateShortcutPreset(ShortcutPreset preset)
    {
        SelectedShortcutPreset = preset;
        SaveSettings();
    }

    public void SaveApiKey(string apiKey)
    {
        var trimmed = apiKey.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            ClearApiKey();
            return;
        }

        providerRegistry.SaveKey(trimmed, SelectedProvider);
        ApiKeyInput = trimmed;
        ProviderStatusMessage = $"Saved {SelectedProvider.DisplayName()} API key to secure local storage.";
    }

    public void ClearApiKey()
    {
        providerRegistry.DeleteKey(SelectedProvider);
        ApiKeyInput = string.Empty;
        ProviderStatusMessage = $"Removed {SelectedProvider.DisplayName()} API key from secure local storage.";
    }

    public void SetHotkeyStatusMessage(string message)
    {
        HotkeyStatusMessage = message;
    }

    private void SaveSettings()
    {
        settingsDocument.SelectedProvider = SelectedProvider.ToString();
        settingsDocument.SelectedShortcutPreset = SelectedShortcutPreset.ToString();
        settingsStore.Save(settingsDocument);
    }
}
