using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace SnapLingoWindows.Views;

public sealed partial class MainPage : Page
{
    private enum SettingsSection
    {
        Overview,
        Provider,
        Hotkey,
        Prompt,
    }

    private readonly IReadOnlyList<ProviderChoice> providerChoices;
    private readonly IReadOnlyList<ShortcutChoice> shortcutChoices;
    private bool suppressSelectionEvents;
    private bool hasInitialized;

    public MainViewModel ViewModel { get; }
    public FrameworkElement TitleBarElement => AppTitleBar;

    public MainPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        providerChoices = Enum.GetValues<ProviderKind>()
            .Select(kind => new ProviderChoice(kind, kind.DisplayName()))
            .ToList();
        shortcutChoices = Enum.GetValues<ShortcutPreset>()
            .Select(preset => new ShortcutChoice(preset, preset.DisplayName()))
            .ToList();

        InitializeComponent();
        ConfigureControls();
        ViewModel.PropertyChanged += OnViewModelChanged;
        ViewModel.Workflow.PropertyChanged += OnWorkflowChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SelectSection(SettingsSection.Overview);
        Render();
    }

    private void ConfigureControls()
    {
        ProviderComboBox.DisplayMemberPath = nameof(ProviderChoice.Label);
        ProviderComboBox.ItemsSource = providerChoices;

        ModelComboBox.DisplayMemberPath = nameof(ProviderModelOption.Label);
        PromptComboBox.DisplayMemberPath = nameof(PromptProfile.Name);

        HotkeyComboBox.DisplayMemberPath = nameof(ShortcutChoice.Label);
        HotkeyComboBox.ItemsSource = shortcutChoices;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (hasInitialized)
        {
            return;
        }

        hasInitialized = true;
        await ViewModel.InitializeAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelChanged;
        ViewModel.Workflow.PropertyChanged -= OnWorkflowChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        Render();
    }

    private void OnWorkflowChanged(object? sender, PropertyChangedEventArgs e)
    {
        Render();
    }

    private void Render()
    {
        suppressSelectionEvents = true;

        try
        {
            ProviderComboBox.SelectedItem = providerChoices.FirstOrDefault(choice => choice.Kind == ViewModel.SelectedProvider);
            ModelComboBox.ItemsSource = ViewModel.AvailableModels;
            ModelComboBox.SelectedItem = ViewModel.AvailableModels
                .FirstOrDefault(option => string.Equals(option.Id, ViewModel.SelectedModelId, StringComparison.OrdinalIgnoreCase));
            ModelComboBox.IsEnabled = ViewModel.CanSelectModel;
            ModelComboBox.PlaceholderText = ViewModel.IsLoadingModels
                ? "Loading models..."
                : "Save an API key to load models";
            PromptComboBox.ItemsSource = ViewModel.AvailablePrompts;
            PromptComboBox.SelectedItem = ViewModel.AvailablePrompts
                .FirstOrDefault(prompt => string.Equals(prompt.Id, ViewModel.SelectedPromptId, StringComparison.OrdinalIgnoreCase));
            PromptNameTextBox.IsEnabled = ViewModel.CanEditSelectedPrompt;
            TranslatePromptTextBox.IsEnabled = ViewModel.CanEditSelectedPrompt;
            PolishPromptTextBox.IsEnabled = ViewModel.CanEditSelectedPrompt;
            SavePromptButton.IsEnabled = ViewModel.CanEditSelectedPrompt;
            DeletePromptButton.IsEnabled = ViewModel.CanDeleteSelectedPrompt;
            HotkeyComboBox.SelectedItem = shortcutChoices.FirstOrDefault(choice => choice.Preset == ViewModel.SelectedShortcutPreset);

            if (ApiKeyPasswordBox.Password != ViewModel.ApiKeyInput)
            {
                ApiKeyPasswordBox.Password = ViewModel.ApiKeyInput;
            }

            if (PromptNameTextBox.Text != ViewModel.PromptEditorName)
            {
                PromptNameTextBox.Text = ViewModel.PromptEditorName;
            }

            if (TranslatePromptTextBox.Text != ViewModel.PromptEditorTranslate)
            {
                TranslatePromptTextBox.Text = ViewModel.PromptEditorTranslate;
            }

            if (PolishPromptTextBox.Text != ViewModel.PromptEditorPolish)
            {
                PolishPromptTextBox.Text = ViewModel.PromptEditorPolish;
            }

            ApplyStatusText(ProviderStatusTextBlock, ViewModel.ProviderStatusMessage);
            ApplyStatusText(PromptStatusTextBlock, ViewModel.PromptStatusMessage);
            ApplyStatusText(HotkeyStatusTextBlock, ViewModel.HotkeyStatusMessage);
            OverviewProviderValueTextBlock.Text = ViewModel.SelectedProvider.DisplayName();
            OverviewHotkeyValueTextBlock.Text = ViewModel.SelectedShortcutPreset.DisplayName();
            OverviewWorkflowValueTextBlock.Text = ViewModel.Workflow.Phase == WorkflowPhase.Idle ? "Ready" : "Active";
            ApplyStatusText(OverviewStatusTextBlock, ViewModel.ProviderStatusMessage);
        }
        finally
        {
            suppressSelectionEvents = false;
        }
    }

    private void OnNavigationClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tag || !Enum.TryParse(tag, out SettingsSection section))
        {
            return;
        }

        SelectSection(section);
    }

    private void OnShowPanelClicked(object sender, RoutedEventArgs e)
    {
        (Application.Current as App)?.ShowTranslationPanel();
    }

    private async void OnUseClipboardClicked(object sender, RoutedEventArgs e)
    {
        (Application.Current as App)?.ShowTranslationPanel();
        await ViewModel.UseCurrentClipboardAsync();
    }

    private async void OnProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelectionEvents || ProviderComboBox.SelectedItem is not ProviderChoice choice)
        {
            return;
        }

        await ViewModel.UpdateProviderAsync(choice.Kind);
    }

    private void OnModelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelectionEvents || ModelComboBox.SelectedItem is not ProviderModelOption option)
        {
            return;
        }

        ViewModel.UpdateSelectedModel(option.Id);
    }

    private void OnHotkeyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelectionEvents || HotkeyComboBox.SelectedItem is not ShortcutChoice choice)
        {
            return;
        }

        ViewModel.UpdateShortcutPreset(choice.Preset);
        (Application.Current as App)?.SettingsWindow.ApplyHotkeyPreset(choice.Preset);
    }

    private void OnPromptChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelectionEvents || PromptComboBox.SelectedItem is not PromptProfile prompt)
        {
            return;
        }

        ViewModel.UpdateSelectedPrompt(prompt.Id);
    }

    private void OnPromptNameChanged(object sender, TextChangedEventArgs e)
    {
        if (suppressSelectionEvents)
        {
            return;
        }

        ViewModel.UpdatePromptEditorName(PromptNameTextBox.Text);
    }

    private void OnTranslatePromptChanged(object sender, TextChangedEventArgs e)
    {
        if (suppressSelectionEvents)
        {
            return;
        }

        ViewModel.UpdatePromptEditorTranslate(TranslatePromptTextBox.Text);
    }

    private void OnPolishPromptChanged(object sender, TextChangedEventArgs e)
    {
        if (suppressSelectionEvents)
        {
            return;
        }

        ViewModel.UpdatePromptEditorPolish(PolishPromptTextBox.Text);
    }

    private void OnCreatePromptClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.CreatePrompt();
    }

    private void OnSavePromptClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.SavePrompt();
    }

    private void OnDeletePromptClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.DeleteSelectedPrompt();
    }

    private async void OnSaveKeyClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveApiKeyAsync(ApiKeyPasswordBox.Password);
    }

    private void OnClearKeyClicked(object sender, RoutedEventArgs e)
    {
        ApiKeyPasswordBox.Password = string.Empty;
        ViewModel.ClearApiKey();
    }

    private void SelectSection(SettingsSection section)
    {
        OverviewPanel.Visibility = section == SettingsSection.Overview ? Visibility.Visible : Visibility.Collapsed;
        ProviderPanel.Visibility = section == SettingsSection.Provider ? Visibility.Visible : Visibility.Collapsed;
        HotkeyPanel.Visibility = section == SettingsSection.Hotkey ? Visibility.Visible : Visibility.Collapsed;
        PromptPanel.Visibility = section == SettingsSection.Prompt ? Visibility.Visible : Visibility.Collapsed;

        ApplyNavigationState(OverviewNavButton, OverviewNavAccent, section == SettingsSection.Overview);
        ApplyNavigationState(ProviderNavButton, ProviderNavAccent, section == SettingsSection.Provider);
        ApplyNavigationState(HotkeyNavButton, HotkeyNavAccent, section == SettingsSection.Hotkey);
        ApplyNavigationState(PromptNavButton, PromptNavAccent, section == SettingsSection.Prompt);
    }

    private void ApplyNavigationState(Button button, Border accent, bool isSelected)
    {
        button.Background = isSelected
            ? (Brush)Resources["NavSelectedBackgroundBrush"]
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        button.BorderBrush = isSelected
            ? (Brush)Resources["NavSelectedBorderBrush"]
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        accent.Background = isSelected
            ? (Brush)Resources["NavSelectedAccentBrush"]
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private static void ApplyStatusText(TextBlock textBlock, string? message)
    {
        var hasMessage = !string.IsNullOrWhiteSpace(message);
        textBlock.Text = hasMessage ? message : string.Empty;
        textBlock.Visibility = hasMessage ? Visibility.Visible : Visibility.Collapsed;
    }

    private sealed record ProviderChoice(ProviderKind Kind, string Label);
    private sealed record ShortcutChoice(ShortcutPreset Preset, string Label);
}
