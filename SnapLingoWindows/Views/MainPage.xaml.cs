using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace SnapLingoWindows.Views;

public sealed partial class MainPage : Page
{
    private enum SettingsSection
    {
        Basic,
        Provider,
        Prompt,
        Appearance,
    }

    private readonly IReadOnlyList<ProviderChoice> providerChoices;
    private readonly IReadOnlyList<ShortcutChoice> shortcutChoices;
    private readonly IReadOnlyList<LanguageChoice> languageChoices;
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
        languageChoices =
        [
            new LanguageChoice(AppLanguage.English, "English"),
            new LanguageChoice(AppLanguage.Chinese, "中文"),
        ];

        InitializeComponent();
        ConfigureControls();
        ViewModel.PropertyChanged += OnViewModelChanged;
        ViewModel.Workflow.PropertyChanged += OnWorkflowChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SelectSection(SettingsSection.Basic);
        Render();
    }

    private void ConfigureControls()
    {
        ProviderComboBox.DisplayMemberPath = nameof(ProviderChoice.Label);
        ProviderComboBox.ItemsSource = providerChoices;

        ModelComboBox.DisplayMemberPath = nameof(ProviderModelOption.Label);
        PromptComboBox.DisplayMemberPath = nameof(PromptChoice.Label);
        LanguageComboBox.DisplayMemberPath = nameof(LanguageChoice.Label);
        LanguageComboBox.ItemsSource = languageChoices;

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
            RenderLocalizedText();
            ProviderComboBox.SelectedItem = providerChoices.FirstOrDefault(choice => choice.Kind == ViewModel.SelectedProvider);
            ModelComboBox.ItemsSource = ViewModel.AvailableModels;
            ModelComboBox.SelectedItem = ViewModel.AvailableModels
                .FirstOrDefault(option => string.Equals(option.Id, ViewModel.SelectedModelId, StringComparison.OrdinalIgnoreCase));
            ModelComboBox.IsEnabled = ViewModel.CanSelectModel;
            ModelComboBox.PlaceholderText = ViewModel.IsLoadingModels
                ? ViewModel.Localizer.Get("loading_models")
                : ViewModel.Localizer.Get("placeholder_load_models");
            var promptChoices = BuildPromptChoices();
            PromptComboBox.ItemsSource = promptChoices;
            PromptComboBox.SelectedItem = promptChoices
                .FirstOrDefault(prompt => string.Equals(prompt.Id, ViewModel.SelectedPromptId, StringComparison.OrdinalIgnoreCase));
            PromptNameTextBox.IsEnabled = ViewModel.CanEditSelectedPrompt;
            TranslatePromptTextBox.IsEnabled = ViewModel.CanEditSelectedPrompt;
            PolishPromptTextBox.IsEnabled = ViewModel.CanEditSelectedPrompt;
            SavePromptButton.IsEnabled = ViewModel.CanEditSelectedPrompt;
            DeletePromptButton.IsEnabled = ViewModel.CanDeleteSelectedPrompt;
            HotkeyComboBox.SelectedItem = shortcutChoices.FirstOrDefault(choice => choice.Preset == ViewModel.SelectedShortcutPreset);
            LanguageComboBox.SelectedItem = languageChoices.FirstOrDefault(choice => choice.Language == ViewModel.SelectedLanguage);

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
            ApplyStatusText(OverviewStatusTextBlock, ViewModel.HotkeyStatusMessage ?? ViewModel.ProviderStatusMessage);
        }
        finally
        {
            suppressSelectionEvents = false;
        }
    }

    private void RenderLocalizedText()
    {
        SettingsTitleBarTextBlock.Text = ViewModel.Localizer.Get("window_settings_title");
        SidebarAppNameTextBlock.Text = "SnapLingo";
        SidebarSettingsTextBlock.Text = ViewModel.Localizer.Get("sidebar_settings_title");
        SectionsLabelTextBlock.Text = ViewModel.Localizer.Get("sidebar_sections");
        OverviewNavTextBlock.Text = ViewModel.Localizer.Get("nav_basic");
        ProviderNavTextBlock.Text = ViewModel.Localizer.Get("nav_provider");
        PromptNavTextBlock.Text = ViewModel.Localizer.Get("nav_prompt");
        AppearanceNavTextBlock.Text = ViewModel.Localizer.Get("nav_appearance");

        BasicTitleTextBlock.Text = ViewModel.Localizer.Get("basic_title");
        BasicHintTextBlock.Text = ViewModel.Localizer.Get("basic_hint");
        BasicProviderLabelTextBlock.Text = ViewModel.Localizer.Get("label_provider");
        BasicHotkeyLabelTextBlock.Text = ViewModel.Localizer.Get("label_hotkey");
        BasicStorageLabelTextBlock.Text = ViewModel.Localizer.Get("label_storage");
        BasicWorkflowLabelTextBlock.Text = ViewModel.Localizer.Get("label_workflow");
        StorageValueTextBlock.Text = ViewModel.Localizer.Get("storage_secure_local");
        OverviewWorkflowValueTextBlock.Text = ViewModel.Workflow.Phase == WorkflowPhase.Idle
            ? ViewModel.Localizer.Get("workflow_ready")
            : ViewModel.Localizer.Get("workflow_active");
        QuickActionsLabelTextBlock.Text = ViewModel.Localizer.Get("quick_actions");
        ShowPanelButton.Content = ViewModel.Localizer.Get("button_show_panel");
        UseClipboardButton.Content = ViewModel.Localizer.Get("button_use_clipboard");
        HotkeySectionLabelTextBlock.Text = ViewModel.Localizer.Get("label_hotkey");
        HotkeyHintTextBlock.Text = ViewModel.Localizer.Get("hotkey_hint");

        ProviderTitleTextBlock.Text = ViewModel.Localizer.Get("provider_title");
        ProviderHintTextBlock.Text = ViewModel.Localizer.Get("provider_hint");
        ProviderFieldLabelTextBlock.Text = ViewModel.Localizer.Get("label_provider");
        ModelFieldLabelTextBlock.Text = ViewModel.Localizer.Get("label_model");
        ApiKeyFieldLabelTextBlock.Text = ViewModel.Localizer.Get("label_api_key");
        SaveKeyButton.Content = ViewModel.Localizer.Get("button_save_key");
        ClearKeyButton.Content = ViewModel.Localizer.Get("button_clear_key");

        PromptTitleTextBlock.Text = ViewModel.Localizer.Get("prompt_title");
        PromptHintTextBlock.Text = ViewModel.Localizer.Get("prompt_hint");
        PromptProfileLabelTextBlock.Text = ViewModel.Localizer.Get("prompt_profile");
        CreatePromptButton.Content = ViewModel.Localizer.Get("button_new");
        SavePromptButton.Content = ViewModel.Localizer.Get("button_save");
        DeletePromptButton.Content = ViewModel.Localizer.Get("button_delete");
        PromptNameLabelTextBlock.Text = ViewModel.Localizer.Get("label_name");
        TranslatePromptLabelTextBlock.Text = ViewModel.Localizer.Get("label_translate_prompt");
        PolishPromptLabelTextBlock.Text = ViewModel.Localizer.Get("label_polish_prompt");

        AppearanceTitleTextBlock.Text = ViewModel.Localizer.Get("appearance_title");
        AppearanceHintTextBlock.Text = ViewModel.Localizer.Get("appearance_hint");
        LanguageLabelTextBlock.Text = ViewModel.Localizer.Get("label_language");
    }

    private IReadOnlyList<PromptChoice> BuildPromptChoices()
    {
        return ViewModel.AvailablePrompts
            .Select(prompt => new PromptChoice(
                prompt.Id,
                prompt.IsBuiltIn ? ViewModel.Localizer.Get("prompt_default_name") : prompt.Name))
            .ToList();
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
        if (suppressSelectionEvents || PromptComboBox.SelectedItem is not PromptChoice prompt)
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

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelectionEvents || LanguageComboBox.SelectedItem is not LanguageChoice choice)
        {
            return;
        }

        ViewModel.UpdateLanguage(choice.Language);
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
        OverviewPanel.Visibility = section == SettingsSection.Basic ? Visibility.Visible : Visibility.Collapsed;
        ProviderPanel.Visibility = section == SettingsSection.Provider ? Visibility.Visible : Visibility.Collapsed;
        PromptPanel.Visibility = section == SettingsSection.Prompt ? Visibility.Visible : Visibility.Collapsed;
        AppearancePanel.Visibility = section == SettingsSection.Appearance ? Visibility.Visible : Visibility.Collapsed;

        ApplyNavigationState(OverviewNavButton, OverviewNavAccent, section == SettingsSection.Basic);
        ApplyNavigationState(ProviderNavButton, ProviderNavAccent, section == SettingsSection.Provider);
        ApplyNavigationState(PromptNavButton, PromptNavAccent, section == SettingsSection.Prompt);
        ApplyNavigationState(AppearanceNavButton, AppearanceNavAccent, section == SettingsSection.Appearance);
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
    private sealed record PromptChoice(string Id, string Label);
    private sealed record LanguageChoice(AppLanguage Language, string Label);
}
