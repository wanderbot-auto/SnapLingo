using SnapLingoWindows.Infrastructure;

namespace SnapLingoWindows.ViewModels;

public sealed class AutoSelectionSettingsViewModel : BindableBase
{
    private static readonly int[] DebounceChoiceValues = [120, 200, 280, 360, 500];
    private static readonly int[] MinimumWordChoiceValues = [2, 3, 4, 5, 6];

    private readonly LocalizationService localizer;
    private readonly AppSettingsDocument settingsDocument;
    private readonly Action saveSettings;
    private IReadOnlyList<DebounceChoice> debounceChoices = [];
    private IReadOnlyList<MinimumWordChoice> minimumWordChoices = [];
    private DebounceChoice? selectedDebounceChoice;
    private MinimumWordChoice? selectedMinimumWordChoice;

    public AutoSelectionSettingsViewModel(
        LocalizationService localizer,
        AppSettingsDocument settingsDocument,
        Action saveSettings)
    {
        this.localizer = localizer;
        this.settingsDocument = settingsDocument;
        this.saveSettings = saveSettings;
        this.localizer.LanguageChanged += OnLanguageChanged;

        debounceChoices = BuildDebounceChoices();
        minimumWordChoices = BuildMinimumWordChoices();

        var initialSettings = SelectionActivationSettings.Create(
            settingsDocument.AutoSelectionDebounceMilliseconds,
            settingsDocument.AutoSelectionMinimumWordCount);
        selectedDebounceChoice = ResolveDebounceChoice(initialSettings);
        selectedMinimumWordChoice = ResolveMinimumWordChoice(initialSettings);

        settingsDocument.AutoSelectionDebounceMilliseconds = selectedDebounceChoice.Milliseconds;
        settingsDocument.AutoSelectionMinimumWordCount = selectedMinimumWordChoice.WordCount;
    }

    public IReadOnlyList<DebounceChoice> DebounceChoices
    {
        get => debounceChoices;
        private set => SetProperty(ref debounceChoices, value);
    }

    public IReadOnlyList<MinimumWordChoice> MinimumWordChoices
    {
        get => minimumWordChoices;
        private set => SetProperty(ref minimumWordChoices, value);
    }

    public DebounceChoice? SelectedDebounceChoice
    {
        get => selectedDebounceChoice;
        set
        {
            if (!SetProperty(ref selectedDebounceChoice, value) || value is null)
            {
                return;
            }

            settingsDocument.AutoSelectionDebounceMilliseconds = value.Milliseconds;
            OnPropertyChanged(nameof(CurrentBehaviorSettings));
            OnPropertyChanged(nameof(BehaviorSummaryText));
            saveSettings();
        }
    }

    public MinimumWordChoice? SelectedMinimumWordChoice
    {
        get => selectedMinimumWordChoice;
        set
        {
            if (!SetProperty(ref selectedMinimumWordChoice, value) || value is null)
            {
                return;
            }

            settingsDocument.AutoSelectionMinimumWordCount = value.WordCount;
            OnPropertyChanged(nameof(CurrentBehaviorSettings));
            OnPropertyChanged(nameof(BehaviorSummaryText));
            saveSettings();
        }
    }

    public SelectionActivationSettings CurrentBehaviorSettings =>
        SelectionActivationSettings.Create(
            selectedDebounceChoice?.Milliseconds ?? SelectionActivationSettings.DefaultDebounceMilliseconds,
            selectedMinimumWordChoice?.WordCount ?? SelectionActivationSettings.DefaultMinimumWordCount);

    public string BehaviorSummaryText =>
        localizer.Format(
            "auto_selection_summary",
            selectedDebounceChoice?.Milliseconds ?? SelectionActivationSettings.DefaultDebounceMilliseconds,
            selectedMinimumWordChoice?.WordCount ?? SelectionActivationSettings.DefaultMinimumWordCount);

    private IReadOnlyList<DebounceChoice> BuildDebounceChoices()
    {
        return DebounceChoiceValues
            .Select(milliseconds => new DebounceChoice(
                milliseconds,
                localizer.Format("auto_selection_debounce_choice", milliseconds)))
            .ToList();
    }

    private IReadOnlyList<MinimumWordChoice> BuildMinimumWordChoices()
    {
        return MinimumWordChoiceValues
            .Select(wordCount => new MinimumWordChoice(
                wordCount,
                localizer.Format("auto_selection_min_words_choice", wordCount)))
            .ToList();
    }

    private DebounceChoice ResolveDebounceChoice(SelectionActivationSettings settings)
    {
        var debounceMilliseconds = (int)settings.DebounceWindow.TotalMilliseconds;
        return debounceChoices.FirstOrDefault(choice => choice.Milliseconds == debounceMilliseconds)
            ?? debounceChoices.First(choice => choice.Milliseconds == SelectionActivationSettings.DefaultDebounceMilliseconds);
    }

    private MinimumWordChoice ResolveMinimumWordChoice(SelectionActivationSettings settings)
    {
        return minimumWordChoices.FirstOrDefault(choice => choice.WordCount == settings.MinimumWordCount)
            ?? minimumWordChoices.First(choice => choice.WordCount == SelectionActivationSettings.DefaultMinimumWordCount);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        DebounceChoices = BuildDebounceChoices();
        MinimumWordChoices = BuildMinimumWordChoices();
        selectedDebounceChoice = ResolveDebounceChoice(CurrentBehaviorSettings);
        selectedMinimumWordChoice = ResolveMinimumWordChoice(CurrentBehaviorSettings);
        OnPropertyChanged(nameof(SelectedDebounceChoice));
        OnPropertyChanged(nameof(SelectedMinimumWordChoice));
        OnPropertyChanged(nameof(BehaviorSummaryText));
    }
}

public sealed record DebounceChoice(int Milliseconds, string Label);

public sealed record MinimumWordChoice(int WordCount, string Label);
