using SnapLingoWindows.Infrastructure;

namespace SnapLingoWindows.ViewModels;

public sealed class AppearanceSettingsViewModel : BindableBase
{
    private readonly LocalizationService localizer;
    private readonly AppSettingsDocument settingsDocument;
    private readonly Action saveSettings;
    private IReadOnlyList<LanguageChoice> languageChoices = [];
    private LanguageChoice? selectedLanguageChoice;

    public AppearanceSettingsViewModel(
        LocalizationService localizer,
        AppSettingsDocument settingsDocument,
        Action saveSettings,
        AppLanguage initialLanguage)
    {
        this.localizer = localizer;
        this.settingsDocument = settingsDocument;
        this.saveSettings = saveSettings;
        this.localizer.LanguageChanged += OnLanguageChanged;

        languageChoices = BuildLanguageChoices();
        selectedLanguageChoice = languageChoices.FirstOrDefault(choice => choice.Language == initialLanguage)
            ?? languageChoices.First();
        settingsDocument.SelectedLanguage = selectedLanguageChoice.Language.ToString();
    }

    public IReadOnlyList<LanguageChoice> LanguageChoices
    {
        get => languageChoices;
        private set => SetProperty(ref languageChoices, value);
    }

    public LanguageChoice? SelectedLanguageChoice
    {
        get => selectedLanguageChoice;
        set
        {
            if (!SetProperty(ref selectedLanguageChoice, value) || value is null)
            {
                return;
            }

            ApplySelectedLanguage(value.Language, saveChanges: true);
        }
    }

    public AppLanguage SelectedLanguage => selectedLanguageChoice?.Language ?? AppLanguage.English;

    public string SelectedLanguageLabel => selectedLanguageChoice?.Label ?? string.Empty;

    public void UpdateLanguage(AppLanguage language)
    {
        ApplySelectedLanguage(language, saveChanges: true);
    }

    private void ApplySelectedLanguage(AppLanguage language, bool saveChanges)
    {
        if (SelectedLanguage != language)
        {
            selectedLanguageChoice = LanguageChoices.First(choice => choice.Language == language);
            OnPropertyChanged(nameof(SelectedLanguageChoice));
        }

        localizer.SetLanguage(language);
        settingsDocument.SelectedLanguage = language.ToString();
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(SelectedLanguageLabel));

        if (saveChanges)
        {
            saveSettings();
        }
    }

    private IReadOnlyList<LanguageChoice> BuildLanguageChoices()
    {
        return
        [
            new LanguageChoice(AppLanguage.English, localizer.Get("language_english")),
            new LanguageChoice(AppLanguage.Chinese, localizer.Get("language_chinese")),
        ];
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        LanguageChoices = BuildLanguageChoices();
        selectedLanguageChoice = LanguageChoices.First(choice => choice.Language == SelectedLanguage);
        OnPropertyChanged(nameof(SelectedLanguageChoice));
        OnPropertyChanged(nameof(SelectedLanguageLabel));
    }
}

public sealed record LanguageChoice(AppLanguage Language, string Label);
