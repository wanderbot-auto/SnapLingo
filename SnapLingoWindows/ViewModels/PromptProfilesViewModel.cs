using Microsoft.UI.Xaml;
using SnapLingoWindows.Infrastructure;
using SnapLingoWindows.Models;
using SnapLingoWindows.Services;

namespace SnapLingoWindows.ViewModels;

public sealed class PromptProfilesViewModel : BindableBase
{
    private readonly LocalizationService localizer;
    private readonly ProviderRegistry providerRegistry;
    private readonly AppSettingsDocument settingsDocument;
    private readonly Action saveSettings;
    private IReadOnlyList<PromptProfile> availablePrompts = [];
    private IReadOnlyList<PromptChoice> promptChoices = [];
    private PromptChoice? selectedPromptChoice;
    private string promptEditorName = string.Empty;
    private string promptEditorTranslate = string.Empty;
    private string promptEditorPolish = string.Empty;
    private string? promptStatusMessage;

    public PromptProfilesViewModel(
        LocalizationService localizer,
        ProviderRegistry providerRegistry,
        AppSettingsDocument settingsDocument,
        Action saveSettings)
    {
        this.localizer = localizer;
        this.providerRegistry = providerRegistry;
        this.settingsDocument = settingsDocument;
        this.saveSettings = saveSettings;
        this.localizer.LanguageChanged += OnLanguageChanged;

        AvailablePrompts = LoadPromptProfiles();
        ApplySelectedPrompt(ResolveSavedPromptId(), saveChanges: false, announceSelection: false);
    }

    public IReadOnlyList<PromptProfile> AvailablePrompts
    {
        get => availablePrompts;
        private set
        {
            if (SetProperty(ref availablePrompts, value))
            {
                PromptChoices = BuildPromptChoices(value);
            }
        }
    }

    public IReadOnlyList<PromptChoice> PromptChoices
    {
        get => promptChoices;
        private set => SetProperty(ref promptChoices, value);
    }

    public PromptChoice? SelectedPromptChoice
    {
        get => selectedPromptChoice;
        set
        {
            if (!SetProperty(ref selectedPromptChoice, value) || value is null)
            {
                return;
            }

            ApplySelectedPrompt(value.Id, saveChanges: true, announceSelection: true);
        }
    }

    public string SelectedPromptId => selectedPromptChoice?.Id ?? PromptProfile.DefaultId;

    public PromptProfile SelectedPromptProfile => AvailablePrompts.FirstOrDefault(prompt => string.Equals(prompt.Id, SelectedPromptId, StringComparison.OrdinalIgnoreCase))
        ?? AvailablePrompts.First(prompt => string.Equals(prompt.Id, PromptProfile.DefaultId, StringComparison.OrdinalIgnoreCase));

    public bool CanEditSelectedPrompt => !SelectedPromptProfile.IsBuiltIn;

    public bool CanDeleteSelectedPrompt => !SelectedPromptProfile.IsBuiltIn;

    public string PromptEditorName
    {
        get => promptEditorName;
        set => SetProperty(ref promptEditorName, value);
    }

    public string PromptEditorTranslate
    {
        get => promptEditorTranslate;
        set => SetProperty(ref promptEditorTranslate, value);
    }

    public string PromptEditorPolish
    {
        get => promptEditorPolish;
        set => SetProperty(ref promptEditorPolish, value);
    }

    public string? PromptStatusMessage
    {
        get => promptStatusMessage;
        private set
        {
            if (SetProperty(ref promptStatusMessage, value))
            {
                OnPropertyChanged(nameof(PromptStatusVisibility));
            }
        }
    }

    public Visibility PromptStatusVisibility =>
        string.IsNullOrWhiteSpace(PromptStatusMessage) ? Visibility.Collapsed : Visibility.Visible;

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
        ApplySelectedPrompt(prompt.Id, saveChanges: true, announceSelection: false);
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

        ApplySelectedPrompt(updatedPrompt.Id, saveChanges: true, announceSelection: false);
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

        ApplySelectedPrompt(PromptProfile.DefaultId, saveChanges: true, announceSelection: false);
        PromptStatusMessage = localizer.Format("status_deleted_prompt", deletedPromptName);
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

    private IReadOnlyList<PromptChoice> BuildPromptChoices(IReadOnlyList<PromptProfile> prompts)
    {
        return prompts.Select(prompt => new PromptChoice(
            prompt.Id,
            prompt.IsBuiltIn ? localizer.Get("prompt_default_name") : prompt.Name)).ToList();
    }

    private string ResolveSavedPromptId()
    {
        return AvailablePrompts.Any(prompt => string.Equals(prompt.Id, settingsDocument.SelectedPromptId, StringComparison.OrdinalIgnoreCase))
            ? settingsDocument.SelectedPromptId
            : PromptProfile.DefaultId;
    }

    private void ApplySelectedPrompt(string promptId, bool saveChanges, bool announceSelection)
    {
        var selectedPrompt = AvailablePrompts.FirstOrDefault(prompt => string.Equals(prompt.Id, promptId, StringComparison.OrdinalIgnoreCase))
            ?? AvailablePrompts.First(prompt => string.Equals(prompt.Id, PromptProfile.DefaultId, StringComparison.OrdinalIgnoreCase));

        selectedPromptChoice = PromptChoices.First(choice => string.Equals(choice.Id, selectedPrompt.Id, StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(SelectedPromptChoice));
        OnPropertyChanged(nameof(SelectedPromptId));

        PromptEditorName = GetPromptDisplayName(selectedPrompt);
        PromptEditorTranslate = selectedPrompt.TranslatePrompt;
        PromptEditorPolish = selectedPrompt.PolishPrompt;
        providerRegistry.SetSelectedPrompt(selectedPrompt);

        OnPropertyChanged(nameof(SelectedPromptProfile));
        OnPropertyChanged(nameof(CanEditSelectedPrompt));
        OnPropertyChanged(nameof(CanDeleteSelectedPrompt));

        settingsDocument.SelectedPromptId = selectedPrompt.Id;

        if (announceSelection)
        {
            PromptStatusMessage = localizer.Format("status_using_prompt", GetPromptDisplayName(selectedPrompt));
        }

        if (saveChanges)
        {
            SaveSettings();
        }
    }

    private void SaveSettings()
    {
        settingsDocument.PromptProfiles = AvailablePrompts
            .Where(prompt => !prompt.IsBuiltIn)
            .ToList();
        saveSettings();
    }

    private string GeneratePromptName()
    {
        var baseName = localizer.Get("prompt_custom_name_base");
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

    private string GetPromptDisplayName(PromptProfile prompt)
    {
        return prompt.IsBuiltIn ? localizer.Get("prompt_default_name") : prompt.Name;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        PromptChoices = BuildPromptChoices(AvailablePrompts);
        selectedPromptChoice = PromptChoices.First(choice => string.Equals(choice.Id, SelectedPromptId, StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(SelectedPromptChoice));

        if (SelectedPromptProfile.IsBuiltIn)
        {
            PromptEditorName = GetPromptDisplayName(SelectedPromptProfile);
        }

        PromptStatusMessage = null;
    }
}

public sealed record PromptChoice(string Id, string Label);
