using SnapLingoWindows.Infrastructure;
using SnapLingoWindows.Services;
using Windows.ApplicationModel.DataTransfer;

namespace SnapLingoWindows.ViewModels;

public sealed class MainViewModel : BindableBase
{
    private readonly JsonSettingsStore settingsStore;
    private readonly LocalizationService localizer;
    private readonly ProviderRegistry providerRegistry;
    private readonly WorkflowOrchestrator orchestrator;
    private readonly AppSettingsDocument settingsDocument;

    private AppLanguage selectedLanguage;
    private ProviderKind selectedProvider;
    private ShortcutPreset selectedShortcutPreset;
    private string apiKeyInput = string.Empty;
    private string? providerStatusMessage;
    private string? promptStatusMessage;
    private string? hotkeyStatusMessage;
    private IReadOnlyList<ProviderModelOption> availableModels = [];
    private IReadOnlyList<PromptProfile> availablePrompts = [];
    private string selectedModelId = string.Empty;
    private string selectedPromptId = PromptProfile.DefaultId;
    private string promptEditorName = string.Empty;
    private string promptEditorTranslate = string.Empty;
    private string promptEditorPolish = string.Empty;
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

        if (!Enum.TryParse(settingsDocument.SelectedLanguage, out selectedLanguage))
        {
            selectedLanguage = AppLanguage.English;
        }

        localizer = new LocalizationService(selectedLanguage);
        localizer.LanguageChanged += OnLanguageChanged;
        var secretStore = new SecureSecretStore();
        providerRegistry = new ProviderRegistry(secretStore, localizer)
        {
            SelectedProvider = selectedProvider,
        };
        availablePrompts = LoadPromptProfiles();
        selectedPromptId = ResolveSavedPromptId();
        ApplySelectedPrompt(selectedPromptId, saveSettings: false, announceSelection: false);
        RestoreSelectedModels();
        Workflow = new WorkflowStateStore(localizer);
        Workflow.ResetForIdle();
        orchestrator = new WorkflowOrchestrator(Workflow, new ClipboardSelectionCaptureService(), providerRegistry, localizer);
        apiKeyInput = providerRegistry.LoadKey(selectedProvider);
        selectedModelId = ResolveSavedModel(selectedProvider);
        availableModels = BuildFallbackModels(selectedProvider, selectedModelId);
    }

    public event EventHandler? HideRequested;

    public WorkflowStateStore Workflow { get; }
    public LocalizationService Localizer => localizer;

    public AppLanguage SelectedLanguage
    {
        get => selectedLanguage;
        private set => SetProperty(ref selectedLanguage, value);
    }

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

    public string? PromptStatusMessage
    {
        get => promptStatusMessage;
        private set => SetProperty(ref promptStatusMessage, value);
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

    public IReadOnlyList<PromptProfile> AvailablePrompts
    {
        get => availablePrompts;
        private set => SetProperty(ref availablePrompts, value);
    }

    public string SelectedModelId
    {
        get => selectedModelId;
        private set => SetProperty(ref selectedModelId, value);
    }

    public string SelectedPromptId
    {
        get => selectedPromptId;
        private set => SetProperty(ref selectedPromptId, value);
    }

    public string PromptEditorName
    {
        get => promptEditorName;
        private set => SetProperty(ref promptEditorName, value);
    }

    public string PromptEditorTranslate
    {
        get => promptEditorTranslate;
        private set => SetProperty(ref promptEditorTranslate, value);
    }

    public string PromptEditorPolish
    {
        get => promptEditorPolish;
        private set => SetProperty(ref promptEditorPolish, value);
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
    public PromptProfile SelectedPromptProfile => ResolveSelectedPromptProfile();
    public bool CanEditSelectedPrompt => !SelectedPromptProfile.IsBuiltIn;
    public bool CanDeleteSelectedPrompt => !SelectedPromptProfile.IsBuiltIn;

    public async Task InitializeAsync()
    {
        await RefreshModelsAsync(preferLatest: false, updateStatusWhenMissingKey: false);
    }

    public async Task HandleHotkeyAsync()
    {
        await orchestrator.HandleHotkeyAsync();
    }

    public async Task HandleDetectedSelectionAsync(string text)
    {
        await orchestrator.HandleCapturedSelectionAsync(text);
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
        ProviderStatusMessage = localizer.Format("status_switched_provider", provider.DisplayName());
        SaveSettings();
        await RefreshModelsAsync(preferLatest: false, updateStatusWhenMissingKey: false);
    }

    public void UpdateShortcutPreset(ShortcutPreset preset)
    {
        SelectedShortcutPreset = preset;
        SaveSettings();
    }

    public void UpdateLanguage(AppLanguage language)
    {
        localizer.SetLanguage(language);
        SelectedLanguage = language;
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
        ProviderStatusMessage = localizer.Format("status_saved_key_loading", SelectedProvider.DisplayName());
        await RefreshModelsAsync(preferLatest: true, updateStatusWhenMissingKey: true);
    }

    public void ClearApiKey()
    {
        providerRegistry.DeleteKey(SelectedProvider);
        ApiKeyInput = string.Empty;
        AvailableModels = BuildFallbackModels(SelectedProvider, SelectedModelId);
        ProviderStatusMessage = localizer.Format("status_removed_key", SelectedProvider.DisplayName());
    }

    public void UpdateSelectedModel(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return;
        }

        ApplySelectedModel(SelectedProvider, modelId, saveSettings: true);
        ProviderStatusMessage = localizer.Format("status_using_model", modelId);
    }

    public void UpdateSelectedPrompt(string promptId)
    {
        ApplySelectedPrompt(promptId, saveSettings: true, announceSelection: true);
    }

    public void UpdatePromptEditorName(string value)
    {
        PromptEditorName = value;
    }

    public void UpdatePromptEditorTranslate(string value)
    {
        PromptEditorTranslate = value;
    }

    public void UpdatePromptEditorPolish(string value)
    {
        PromptEditorPolish = value;
    }

    public void CreatePrompt()
    {
        var template = SelectedPromptProfile;
        var prompt = new PromptProfile(
            Guid.NewGuid().ToString("N"),
            GeneratePromptName(),
            template.TranslatePrompt,
            template.PolishPrompt
        );

        AvailablePrompts = AvailablePrompts.Concat([prompt]).ToList();
        ApplySelectedPrompt(prompt.Id, saveSettings: true, announceSelection: false);
        PromptStatusMessage = localizer.Format("status_created_prompt", prompt.Name);
    }

    public void SavePrompt()
    {
        if (SelectedPromptProfile.IsBuiltIn)
        {
            PromptStatusMessage = localizer.Get("status_default_prompt_readonly");
            return;
        }

        var name = PromptEditorName.Trim();
        var translatePrompt = PromptEditorTranslate.Trim();
        var polishPrompt = PromptEditorPolish.Trim();

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(translatePrompt) ||
            string.IsNullOrWhiteSpace(polishPrompt))
        {
            PromptStatusMessage = localizer.Get("status_prompt_fields_required");
            return;
        }

        var updatedPrompt = SelectedPromptProfile with
        {
            Name = name,
            TranslatePrompt = translatePrompt,
            PolishPrompt = polishPrompt,
        };

        AvailablePrompts = AvailablePrompts
            .Select(prompt => string.Equals(prompt.Id, updatedPrompt.Id, StringComparison.OrdinalIgnoreCase) ? updatedPrompt : prompt)
            .ToList();

        ApplySelectedPrompt(updatedPrompt.Id, saveSettings: true, announceSelection: false);
        PromptStatusMessage = localizer.Format("status_saved_prompt", updatedPrompt.Name);
    }

    public void DeleteSelectedPrompt()
    {
        if (SelectedPromptProfile.IsBuiltIn)
        {
            PromptStatusMessage = localizer.Get("status_default_prompt_undeletable");
            return;
        }

        var deletedPromptName = SelectedPromptProfile.Name;
        AvailablePrompts = AvailablePrompts
            .Where(prompt => !string.Equals(prompt.Id, SelectedPromptId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        ApplySelectedPrompt(PromptProfile.DefaultId, saveSettings: true, announceSelection: false);
        PromptStatusMessage = localizer.Format("status_deleted_prompt", deletedPromptName);
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
                ProviderStatusMessage = localizer.Get("status_save_key_first");
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
            ProviderStatusMessage = localizer.Format("status_loaded_models", catalog.Models.Count, SelectedProvider.DisplayName());
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

    private IReadOnlyList<PromptProfile> LoadPromptProfiles()
    {
        var prompts = new List<PromptProfile>
        {
            PromptProfile.CreateDefault(),
        };
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PromptProfile.DefaultId,
        };

        foreach (var savedPrompt in settingsDocument.PromptProfiles ?? [])
        {
            if (savedPrompt is null)
            {
                continue;
            }

            var id = savedPrompt.Id?.Trim();
            var name = savedPrompt.Name?.Trim();
            var translatePrompt = savedPrompt.TranslatePrompt?.Trim();
            var polishPrompt = savedPrompt.PolishPrompt?.Trim();

            if (string.IsNullOrWhiteSpace(id) ||
                string.Equals(id, PromptProfile.DefaultId, StringComparison.OrdinalIgnoreCase) ||
                !usedIds.Add(id) ||
                string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(translatePrompt) ||
                string.IsNullOrWhiteSpace(polishPrompt))
            {
                continue;
            }

            prompts.Add(new PromptProfile(id, name, translatePrompt, polishPrompt));
        }

        return prompts;
    }

    private string ResolveSavedPromptId()
    {
        return AvailablePrompts.Any(prompt => string.Equals(prompt.Id, settingsDocument.SelectedPromptId, StringComparison.OrdinalIgnoreCase))
            ? settingsDocument.SelectedPromptId
            : PromptProfile.DefaultId;
    }

    private PromptProfile ResolveSelectedPromptProfile()
    {
        return AvailablePrompts.FirstOrDefault(prompt => string.Equals(prompt.Id, SelectedPromptId, StringComparison.OrdinalIgnoreCase))
            ?? AvailablePrompts.First(prompt => string.Equals(prompt.Id, PromptProfile.DefaultId, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplySelectedPrompt(string promptId, bool saveSettings, bool announceSelection)
    {
        var selectedPrompt = AvailablePrompts.FirstOrDefault(prompt => string.Equals(prompt.Id, promptId, StringComparison.OrdinalIgnoreCase))
            ?? AvailablePrompts.First(prompt => string.Equals(prompt.Id, PromptProfile.DefaultId, StringComparison.OrdinalIgnoreCase));

        SelectedPromptId = selectedPrompt.Id;
        PromptEditorName = GetPromptDisplayName(selectedPrompt);
        PromptEditorTranslate = selectedPrompt.TranslatePrompt;
        PromptEditorPolish = selectedPrompt.PolishPrompt;
        providerRegistry.SetSelectedPrompt(selectedPrompt);

        OnPropertyChanged(nameof(SelectedPromptProfile));
        OnPropertyChanged(nameof(CanEditSelectedPrompt));
        OnPropertyChanged(nameof(CanDeleteSelectedPrompt));

        if (announceSelection)
        {
            PromptStatusMessage = localizer.Format("status_using_prompt", GetPromptDisplayName(selectedPrompt));
        }

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

    private string GeneratePromptName()
    {
        var baseName = localizer.CurrentLanguage == AppLanguage.Chinese ? "自定义提示词" : "Custom Prompt";
        var usedNames = AvailablePrompts
            .Select(prompt => prompt.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!usedNames.Contains(baseName))
        {
            return baseName;
        }

        var index = 2;
        while (usedNames.Contains($"{baseName} {index}"))
        {
            index++;
        }

        return $"{baseName} {index}";
    }

    private void SaveSettings()
    {
        settingsDocument.SelectedLanguage = SelectedLanguage.ToString();
        settingsDocument.SelectedProvider = SelectedProvider.ToString();
        settingsDocument.SelectedShortcutPreset = SelectedShortcutPreset.ToString();
        settingsDocument.SelectedPromptId = SelectedPromptId;
        settingsDocument.PromptProfiles = AvailablePrompts
            .Where(prompt => !prompt.IsBuiltIn)
            .ToList();
        settingsStore.Save(settingsDocument);
    }

    private string GetPromptDisplayName(PromptProfile prompt)
    {
        return prompt.IsBuiltIn ? localizer.Get("prompt_default_name") : prompt.Name;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        providerRegistry.SetSelectedPrompt(SelectedPromptProfile);
        PromptEditorName = GetPromptDisplayName(SelectedPromptProfile);
        ProviderStatusMessage = null;
        PromptStatusMessage = null;
        HotkeyStatusMessage = null;
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(SelectedPromptProfile));
    }
}
