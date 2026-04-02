using Microsoft.UI.Xaml;
using SnapLingoWindows.Infrastructure;
using SnapLingoWindows.Services;

namespace SnapLingoWindows.ViewModels;

public sealed class ProviderSettingsViewModel : BindableBase
{
    private readonly LocalizationService localizer;
    private readonly ProviderRegistry providerRegistry;
    private readonly AppSettingsDocument settingsDocument;
    private readonly Action saveSettings;
    private ProviderChoice? selectedProviderChoice;
    private ProviderModelOption? selectedModelOption;
    private string apiKeyInput = string.Empty;
    private string? providerStatusMessage;
    private IReadOnlyList<ProviderModelOption> availableModels = [];
    private bool isLoadingModels;

    public ProviderSettingsViewModel(
        LocalizationService localizer,
        ProviderRegistry providerRegistry,
        AppSettingsDocument settingsDocument,
        Action saveSettings,
        ProviderKind initialProvider)
    {
        this.localizer = localizer;
        this.providerRegistry = providerRegistry;
        this.settingsDocument = settingsDocument;
        this.saveSettings = saveSettings;
        this.localizer.LanguageChanged += OnLanguageChanged;

        ProviderChoices = Enum.GetValues<ProviderKind>()
            .Select(kind => new ProviderChoice(kind, kind.DisplayName()))
            .ToList();

        providerRegistry.SelectedProvider = initialProvider;
        ApplySelectedProvider(initialProvider, refreshModels: false, announceSelection: false);
    }

    public IReadOnlyList<ProviderChoice> ProviderChoices { get; }

    public ProviderChoice? SelectedProviderChoice
    {
        get => selectedProviderChoice;
        set
        {
            if (!SetProperty(ref selectedProviderChoice, value) || value is null)
            {
                return;
            }

            _ = SelectProviderAsync(value.Kind);
        }
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

    public ProviderModelOption? SelectedModelOption
    {
        get => selectedModelOption;
        set
        {
            if (!SetProperty(ref selectedModelOption, value) || value is null)
            {
                return;
            }

            ApplySelectedModel(value.Id, saveChanges: true, announceSelection: true);
        }
    }

    public string SelectedModelId => selectedModelOption?.Id ?? string.Empty;

    public string ApiKeyInput
    {
        get => apiKeyInput;
        set
        {
            if (SetProperty(ref apiKeyInput, value))
            {
                OnPropertyChanged(nameof(CanSelectModel));
                OnPropertyChanged(nameof(CredentialStatusText));
            }
        }
    }

    public string? ProviderStatusMessage
    {
        get => providerStatusMessage;
        private set
        {
            if (SetProperty(ref providerStatusMessage, value))
            {
                OnPropertyChanged(nameof(ProviderStatusVisibility));
            }
        }
    }

    public Visibility ProviderStatusVisibility =>
        string.IsNullOrWhiteSpace(ProviderStatusMessage) ? Visibility.Collapsed : Visibility.Visible;

    public bool IsLoadingModels
    {
        get => isLoadingModels;
        private set
        {
            if (SetProperty(ref isLoadingModels, value))
            {
                OnPropertyChanged(nameof(CanSelectModel));
                OnPropertyChanged(nameof(ModelPlaceholderText));
            }
        }
    }

    public bool HasAvailableModels => AvailableModels.Count > 0;

    public bool CanSelectModel => HasAvailableModels && !IsLoadingModels && !string.IsNullOrWhiteSpace(ApiKeyInput);

    public ProviderKind SelectedProvider => selectedProviderChoice?.Kind ?? ProviderKind.OpenAI;

    public string SelectedProviderDisplayName => selectedProviderChoice?.Label ?? SelectedProvider.DisplayName();

    public string ApiKeyPlaceholder => SelectedProvider.ApiKeyPlaceholder();

    public string ModelPlaceholderText => IsLoadingModels
        ? localizer.Get("loading_models")
        : localizer.Get("placeholder_load_models");

    public string CredentialStatusText => string.IsNullOrWhiteSpace(ApiKeyInput)
        ? localizer.Get("status_api_key_missing")
        : localizer.Get("status_api_key_saved");

    public async Task InitializeAsync()
    {
        await RefreshModelsAsync(preferLatest: false, updateStatusWhenMissingKey: false);
    }

    public async Task SaveApiKeyAsync()
    {
        var trimmed = ApiKeyInput.Trim();
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
        SetSelectedModelOption(ResolveSavedModel(SelectedProvider), announceSelection: false);
        ProviderStatusMessage = localizer.Format("status_removed_key", SelectedProvider.DisplayName());
    }

    public void SetProviderStatusMessage(string? message)
    {
        ProviderStatusMessage = message;
    }

    private async Task SelectProviderAsync(ProviderKind provider)
    {
        if (provider == providerRegistry.SelectedProvider)
        {
            return;
        }

        ApplySelectedProvider(provider, refreshModels: true, announceSelection: true);
        await Task.CompletedTask;
    }

    private void ApplySelectedProvider(ProviderKind provider, bool refreshModels, bool announceSelection)
    {
        providerRegistry.SelectedProvider = provider;
        settingsDocument.SelectedProvider = provider.ToString();

        selectedProviderChoice = ProviderChoices.First(choice => choice.Kind == provider);
        OnPropertyChanged(nameof(SelectedProviderChoice));
        OnPropertyChanged(nameof(SelectedProvider));
        OnPropertyChanged(nameof(SelectedProviderDisplayName));
        OnPropertyChanged(nameof(ApiKeyPlaceholder));

        ApiKeyInput = providerRegistry.LoadKey(provider);
        AvailableModels = BuildFallbackModels(provider, ResolveSavedModel(provider));
        SetSelectedModelOption(ResolveSavedModel(provider), announceSelection: false);

        if (announceSelection)
        {
            ProviderStatusMessage = localizer.Format("status_switched_provider", provider.DisplayName());
        }

        saveSettings();

        if (refreshModels)
        {
            _ = RefreshModelsAsync(preferLatest: false, updateStatusWhenMissingKey: false);
        }
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

            ApplySelectedModel(nextModelId, saveChanges: true, announceSelection: false);
            ProviderStatusMessage = localizer.Format("status_loaded_models", catalog.Models.Count, SelectedProvider.DisplayName());
        }
        catch (ProviderException error)
        {
            AvailableModels = BuildFallbackModels(SelectedProvider, SelectedModelId);
            ProviderStatusMessage = error.Message;
            SetSelectedModelOption(ResolveSavedModel(SelectedProvider), announceSelection: false);
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

    private void ApplySelectedModel(string modelId, bool saveChanges, bool announceSelection)
    {
        var normalizedModelId = modelId.Trim();
        providerRegistry.SetSelectedModel(SelectedProvider, normalizedModelId);
        settingsDocument.SelectedModels[SelectedProvider.ToString()] = normalizedModelId;
        SetSelectedModelOption(normalizedModelId, announceSelection);

        if (saveChanges)
        {
            saveSettings();
        }
    }

    private void SetSelectedModelOption(string modelId, bool announceSelection)
    {
        var normalizedModelId = modelId.Trim();
        selectedModelOption = AvailableModels.FirstOrDefault(option => string.Equals(option.Id, normalizedModelId, StringComparison.OrdinalIgnoreCase))
            ?? new ProviderModelOption(normalizedModelId, normalizedModelId);
        OnPropertyChanged(nameof(SelectedModelOption));
        OnPropertyChanged(nameof(SelectedModelId));

        if (announceSelection)
        {
            ProviderStatusMessage = localizer.Format("status_using_model", normalizedModelId);
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

        return presetModels.Select(modelId => new ProviderModelOption(modelId, modelId)).ToList();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ProviderStatusMessage = null;
        OnPropertyChanged(nameof(ModelPlaceholderText));
        OnPropertyChanged(nameof(CredentialStatusText));
    }
}

public sealed record ProviderChoice(ProviderKind Kind, string Label);
