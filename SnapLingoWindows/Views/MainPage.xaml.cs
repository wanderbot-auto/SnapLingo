using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
        Advanced,
    }

    private SettingsSection? currentSection;
    private bool hasInitialized;

    public MainViewModel ViewModel { get; }
    public FrameworkElement TitleBarElement => SidebarDragHandle;

    public MainPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;

        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelChanged;
        ViewModel.Localizer.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        RenderLocalizedText();
        RenderHotkeyBanner(ViewModel.SelectedShortcutPreset);
        SelectSection(SettingsSection.Basic);
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
        ViewModel.Localizer.LanguageChanged -= OnLanguageChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedShortcutPreset))
        {
            RenderHotkeyBanner(ViewModel.SelectedShortcutPreset);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RenderLocalizedText();
    }

    private void RenderLocalizedText()
    {
        SidebarAppNameTextBlock.Text = "SnapLingo";
        SidebarSettingsTextBlock.Text = ViewModel.Localizer.Get("sidebar_settings_title");
        SectionsLabelTextBlock.Text = ViewModel.Localizer.Get("sidebar_sections");
        OverviewNavTextBlock.Text = ViewModel.Localizer.Get("nav_basic");
        ProviderNavTextBlock.Text = ViewModel.Localizer.Get("nav_provider");
        PromptNavTextBlock.Text = ViewModel.Localizer.Get("nav_prompt");
        AppearanceNavTextBlock.Text = ViewModel.Localizer.Get("nav_appearance");
        AdvancedNavTextBlock.Text = ViewModel.Localizer.Get("nav_advanced");
        SidebarFooterTextBlock.Text = ViewModel.Localizer.Get("sidebar_footer_status");

        BasicTitleTextBlock.Text = ViewModel.Localizer.Get("basic_title");
        BasicHintTextBlock.Text = ViewModel.Localizer.Get("basic_hint");
        BasicProviderLabelTextBlock.Text = ViewModel.Localizer.Get("label_provider");
        BasicHotkeyLabelTextBlock.Text = ViewModel.Localizer.Get("label_hotkey");
        BasicStorageLabelTextBlock.Text = ViewModel.Localizer.Get("label_storage");
        BasicWorkflowLabelTextBlock.Text = ViewModel.Localizer.Get("label_workflow");
        QuickActionsLabelTextBlock.Text = ViewModel.Localizer.Get("quick_actions");
        ShowPanelButtonTextBlock.Text = ViewModel.Localizer.Get("button_show_panel");
        UseClipboardButtonTextBlock.Text = ViewModel.Localizer.Get("button_use_clipboard");
        HotkeyBannerPrefixTextBlock.Text = ViewModel.Localizer.Get("hotkey_banner_prefix");
        HotkeyBannerHintTextBlock.Text = ViewModel.Localizer.Get("hotkey_banner_hint");
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

        AdvancedTitleTextBlock.Text = ViewModel.Localizer.Get("advanced_title");
        AdvancedHintTextBlock.Text = ViewModel.Localizer.Get("advanced_hint");
        AutoSelectionTitleTextBlock.Text = ViewModel.Localizer.Get("auto_selection_title");
        AutoSelectionHintTextBlock.Text = ViewModel.Localizer.Get("auto_selection_hint");
        AutoSelectionDebounceLabelTextBlock.Text = ViewModel.Localizer.Get("label_auto_selection_debounce");
        AutoSelectionMinimumWordsLabelTextBlock.Text = ViewModel.Localizer.Get("label_auto_selection_min_words");
        AdvancedProviderLabelTextBlock.Text = ViewModel.Localizer.Get("label_provider");
        AdvancedModelLabelTextBlock.Text = ViewModel.Localizer.Get("label_model");
        AdvancedStorageLabelTextBlock.Text = ViewModel.Localizer.Get("label_storage");
        AdvancedWorkflowLabelTextBlock.Text = ViewModel.Localizer.Get("label_workflow");
        AdvancedCredentialLabelTextBlock.Text = ViewModel.Localizer.Get("label_api_key");
        AdvancedLanguageLabelTextBlock.Text = ViewModel.Localizer.Get("label_language");
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

    private async void OnSaveKeyClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveApiKeyAsync();
    }

    private void OnClearKeyClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearApiKey();
    }

    private void OnCreatePromptClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.PromptProfiles.CreatePrompt();
    }

    private void OnSavePromptClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.PromptProfiles.SavePrompt();
    }

    private void OnDeletePromptClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.PromptProfiles.DeleteSelectedPrompt();
    }

    private void OnHideWindowClicked(object sender, RoutedEventArgs e)
    {
        (Application.Current as App)?.HideSettingsWindow();
    }

    private void OnCloseWindowClicked(object sender, RoutedEventArgs e)
    {
        (Application.Current as App)?.ExitApplication();
    }

    private void OnHideWindowButtonPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ApplyWindowButtonHoverState(HideWindowButton, "WindowControlHoverBrush", "WindowControlHoverForegroundBrush");
    }

    private void OnCloseWindowButtonPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ApplyWindowButtonHoverState(CloseWindowButton, "WindowCloseHoverBrush", "WindowCloseHoverForegroundBrush");
    }

    private void OnWindowButtonPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        button.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x00, 0x00, 0x00, 0x00));
        button.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x4B, 0x55, 0x63));
    }

    private void SelectSection(SettingsSection section)
    {
        if (currentSection == section)
        {
            return;
        }

        currentSection = section;
        OverviewPanel.Visibility = section == SettingsSection.Basic ? Visibility.Visible : Visibility.Collapsed;
        ProviderPanel.Visibility = section == SettingsSection.Provider ? Visibility.Visible : Visibility.Collapsed;
        PromptPanel.Visibility = section == SettingsSection.Prompt ? Visibility.Visible : Visibility.Collapsed;
        AppearancePanel.Visibility = section == SettingsSection.Appearance ? Visibility.Visible : Visibility.Collapsed;
        AdvancedPanel.Visibility = section == SettingsSection.Advanced ? Visibility.Visible : Visibility.Collapsed;

        ApplyNavigationState(OverviewNavButton, OverviewNavAccent, section == SettingsSection.Basic);
        ApplyNavigationState(ProviderNavButton, ProviderNavAccent, section == SettingsSection.Provider);
        ApplyNavigationState(PromptNavButton, PromptNavAccent, section == SettingsSection.Prompt);
        ApplyNavigationState(AppearanceNavButton, AppearanceNavAccent, section == SettingsSection.Appearance);
        ApplyNavigationState(AdvancedNavButton, AdvancedNavAccent, section == SettingsSection.Advanced);
    }

    private void ApplyNavigationState(Button button, Border accent, bool isSelected)
    {
        button.Background = isSelected
            ? (Brush)Resources["NavSelectedBackgroundBrush"]
            : (Brush)Resources["TransparentBrush"];
        button.BorderBrush = isSelected
            ? (Brush)Resources["NavSelectedBorderBrush"]
            : (Brush)Resources["TransparentBrush"];
        button.Foreground = isSelected
            ? (Brush)Resources["SidebarNavSelectedTextBrush"]
            : (Brush)Resources["SidebarNavTextBrush"];
        accent.Background = isSelected
            ? (Brush)Resources["NavSelectedAccentBrush"]
            : (Brush)Resources["TransparentBrush"];
    }

    private void RenderHotkeyBanner(ShortcutPreset preset)
    {
        var parts = preset.DisplayName().Split(" + ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var keyTextBlocks = new[] { HotkeyKeyOneTextBlock, HotkeyKeyTwoTextBlock, HotkeyKeyThreeTextBlock, HotkeyKeyFourTextBlock };
        var plusBlocks = new[] { HotkeyPlusOneTextBlock, HotkeyPlusTwoTextBlock, HotkeyPlusThreeTextBlock };

        for (var index = 0; index < keyTextBlocks.Length; index++)
        {
            keyTextBlocks[index].Text = index < parts.Length ? parts[index] : string.Empty;
        }

        HotkeyKeyFourBorder.Visibility = parts.Length > 3 ? Visibility.Visible : Visibility.Collapsed;

        for (var index = 0; index < plusBlocks.Length; index++)
        {
            plusBlocks[index].Visibility = parts.Length > index + 1 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ApplyWindowButtonHoverState(Button button, string backgroundKey, string foregroundKey)
    {
        if (Resources[backgroundKey] is Brush backgroundBrush)
        {
            button.Background = backgroundBrush;
        }

        if (Resources[foregroundKey] is Brush foregroundBrush)
        {
            button.Foreground = foregroundBrush;
        }
    }
}
