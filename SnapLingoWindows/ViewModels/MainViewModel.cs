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
    private IReadOnlyList<ProviderModelOption> availableModels = [];
    private string selectedModelId = string.Empty;
    private bool isLoadingModels;

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
        RestoreSelectedModels();
        Workflow = new WorkflowStateStore();
        Workflow.ResetForIdle();
        orchestrator = new WorkflowOrchestrator(Workflow, new ClipboardSelectionCaptureService(), providerRegistry);
        apiKeyInput = providerRegistry.LoadKey(selectedProvider);
        selectedModelId = ResolveSavedModel(selectedProvider);
        availableModels = BuildFallbackModels(selectedProvider, selectedModelId);
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
        private set
        {
            if (SetProperty(ref apiKeyInput, value))
            {
                OnPropertyChanged(nameof(CanSelectModel));
            }
        }
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

    public IReadOnlyList<ProviderModelOption> AvailableModels
    {
        get => availableModels;
        private set
        {
            if (SetProperty(ref availableModels, value))
            {
                OnPropertyChanged(nameof(HasAvailableModels));
                OnPropertyChanged(nameof(CanSelectModel));
            }
        }
    }

    public string SelectedModelId
    {
        get => selectedModelId;
        private set => SetProperty(ref selectedModelId, value);
    }

    public bool IsLoadingModels
    {
        get => isLoadingModels;
        private set
        {
            if (SetProperty(ref isLoadingModels, value))
            {
                OnPropertyChanged(nameof(CanSelectModel));
            }
        }
    }

    public bool HasAvailableModels => AvailableModels.Count > 0;
    public bool CanSelectModel => HasAvailableModels && !IsLoadingModels && !string.IsNullOrWhiteSpace(ApiKeyInput);

    public async Task InitializeAsync()
    {
        await RefreshModelsAsync(preferLatest: false, updateStatusWhenMissingKey: false);
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

    public async Task UpdateProviderAsync(ProviderKind provider)
    {
        SelectedProvider = provider;
        providerRegistry.SelectedProvider = provider;
        ApiKeyInput = providerRegistry.LoadKey(provider);
        SelectedModelId = ResolveSavedModel(provider);
        AvailableModels = BuildFallbackModels(provider, SelectedModelId);
        ProviderStatusMessage = $"Switched to {provider.DisplayName()}.";
        SaveSettings();
        await RefreshModelsAsync(preferLatest: false, updateStatusWhenMissingKey: false);
    }

    public void UpdateShortcutPreset(ShortcutPreset preset)
    {
        SelectedShortcutPreset = preset;
        SaveSettings();
    }

    public async Task SaveApiKeyAsync(string apiKey)
    {
        var trimmed = apiKey.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            ClearApiKey();
            return;
        }

        providerRegistry.SaveKey(trimmed, SelectedProvider);
        ApiKeyInput = trimmed;
        ProviderStatusMessage = $"Saved {SelectedProvider.DisplayName()} API key. Loading models...";
        await RefreshModelsAsync(preferLatest: true, updateStatusWhenMissingKey: true);
    }

    public void ClearApiKey()
    {
        providerRegistry.DeleteKey(SelectedProvider);
        ApiKeyInput = string.Empty;
        AvailableModels = BuildFallbackModels(SelectedProvider, SelectedModelId);
        ProviderStatusMessage = $"Removed {SelectedProvider.DisplayName()} API key from secure local storage.";
    }

    public void UpdateSelectedModel(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return;
        }

        ApplySelectedModel(SelectedProvider, modelId, saveSettings: true);
        ProviderStatusMessage = $"Using model {modelId}.";
    }

    public void SetHotkeyStatusMessage(string message)
    {
        HotkeyStatusMessage = message;
    }

    private async Task RefreshModelsAsync(bool preferLatest, bool updateStatusWhenMissingKey)
    {
        if (string.IsNullOrWhiteSpace(ApiKeyInput))
        {
            AvailableModels = BuildFallbackModels(SelectedProvider, SelectedModelId);
            if (updateStatusWhenMissingKey)
            {
                ProviderStatusMessage = "Save an API key first, then SnapLingo can load models automatically.";
            }
            return;
        }

        IsLoadingModels = true;

        try
        {
            var catalog = await providerRegistry.FetchModelsAsync(SelectedProvider);
            AvailableModels = catalog.Models;

            var nextModelId = preferLatest
                ? catalog.DefaultModelId
                : ResolvePreferredModel(catalog);

            ApplySelectedModel(SelectedProvider, nextModelId, saveSettings: true);
            ProviderStatusMessage = $"Loaded {catalog.Models.Count} models for {SelectedProvider.DisplayName()}.";
        }
        catch (ProviderException error)
        {
            AvailableModels = BuildFallbackModels(SelectedProvider, SelectedModelId);
            ProviderStatusMessage = error.Message;
        }
        finally
        {
            IsLoadingModels = false;
        }
    }

    private string ResolvePreferredModel(ProviderModelCatalog catalog)
    {
        if (!string.IsNullOrWhiteSpace(SelectedModelId) &&
            catalog.Models.Any(model => string.Equals(model.Id, SelectedModelId, StringComparison.OrdinalIgnoreCase)))
        {
            return catalog.Models.First(model => string.Equals(model.Id, SelectedModelId, StringComparison.OrdinalIgnoreCase)).Id;
        }

        return catalog.DefaultModelId;
    }

    private void ApplySelectedModel(ProviderKind provider, string modelId, bool saveSettings)
    {
        var normalizedModelId = modelId.Trim();
        SelectedModelId = normalizedModelId;
        providerRegistry.SetSelectedModel(provider, normalizedModelId);
        settingsDocument.SelectedModels[provider.ToString()] = normalizedModelId;

        if (saveSettings)
        {
            SaveSettings();
        }
    }

    private void RestoreSelectedModels()
    {
        foreach (var provider in Enum.GetValues<ProviderKind>())
        {
            providerRegistry.SetSelectedModel(provider, ResolveSavedModel(provider));
        }
    }

    private string ResolveSavedModel(ProviderKind provider)
    {
        if (settingsDocument.SelectedModels.TryGetValue(provider.ToString(), out var savedModel) &&
            !string.IsNullOrWhiteSpace(savedModel))
        {
            return savedModel.Trim();
        }

        return providerRegistry.GetPreset(provider).Model;
    }

    private IReadOnlyList<ProviderModelOption> BuildFallbackModels(ProviderKind provider, string currentModelId)
    {
        var preferredModelId = string.IsNullOrWhiteSpace(currentModelId)
            ? providerRegistry.GetPreset(provider).Model
            : currentModelId.Trim();
        var presetModels = providerRegistry.GetPresetModels(provider)
            .Where(modelId => !string.IsNullOrWhiteSpace(modelId))
            .Select(modelId => modelId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!presetModels.Contains(preferredModelId, StringComparer.OrdinalIgnoreCase))
        {
            presetModels.Insert(0, preferredModelId);
        }

        return presetModels
            .Select(modelId => new ProviderModelOption(modelId, modelId))
            .ToList();
    }

    private void SaveSettings()
    {
        settingsDocument.SelectedProvider = SelectedProvider.ToString();
        settingsDocument.SelectedShortcutPreset = SelectedShortcutPreset.ToString();
        settingsStore.Save(settingsDocument);
    }
}
