using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace SnapLingoWindows.Views;

public sealed partial class MainPage : Page
{
    private enum SettingsSection
    {
        Overview,
        Workflow,
        Provider,
        Hotkey,
    }

    private readonly IReadOnlyList<ProviderChoice> providerChoices;
    private readonly IReadOnlyList<ShortcutChoice> shortcutChoices;
    private bool suppressSelectionEvents;

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
        Unloaded += OnUnloaded;
        SelectSection(SettingsSection.Overview);
        Render();
    }

    private void ConfigureControls()
    {
        ProviderComboBox.DisplayMemberPath = nameof(ProviderChoice.Label);
        ProviderComboBox.ItemsSource = providerChoices;

        HotkeyComboBox.DisplayMemberPath = nameof(ShortcutChoice.Label);
        HotkeyComboBox.ItemsSource = shortcutChoices;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelChanged;
        ViewModel.Workflow.PropertyChanged -= OnWorkflowChanged;
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
            HotkeyComboBox.SelectedItem = shortcutChoices.FirstOrDefault(choice => choice.Preset == ViewModel.SelectedShortcutPreset);
            HotkeySummaryTextBlock.Text = $"Current trigger: {ViewModel.SelectedShortcutPreset.DisplayName()}";

            if (ApiKeyPasswordBox.Password != ViewModel.ApiKeyInput)
            {
                ApiKeyPasswordBox.Password = ViewModel.ApiKeyInput;
            }

            ApplyStatusText(ProviderStatusTextBlock, ViewModel.ProviderStatusMessage);
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

    private void OnProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelectionEvents || ProviderComboBox.SelectedItem is not ProviderChoice choice)
        {
            return;
        }

        ViewModel.UpdateProvider(choice.Kind);
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

    private void OnSaveKeyClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveApiKey(ApiKeyPasswordBox.Password);
    }

    private void OnClearKeyClicked(object sender, RoutedEventArgs e)
    {
        ApiKeyPasswordBox.Password = string.Empty;
        ViewModel.ClearApiKey();
    }

    private void SelectSection(SettingsSection section)
    {
        OverviewPanel.Visibility = section == SettingsSection.Overview ? Visibility.Visible : Visibility.Collapsed;
        WorkflowPanel.Visibility = section == SettingsSection.Workflow ? Visibility.Visible : Visibility.Collapsed;
        ProviderPanel.Visibility = section == SettingsSection.Provider ? Visibility.Visible : Visibility.Collapsed;
        HotkeyPanel.Visibility = section == SettingsSection.Hotkey ? Visibility.Visible : Visibility.Collapsed;

        ApplyNavigationState(OverviewNavButton, OverviewNavAccent, section == SettingsSection.Overview);
        ApplyNavigationState(WorkflowNavButton, WorkflowNavAccent, section == SettingsSection.Workflow);
        ApplyNavigationState(ProviderNavButton, ProviderNavAccent, section == SettingsSection.Provider);
        ApplyNavigationState(HotkeyNavButton, HotkeyNavAccent, section == SettingsSection.Hotkey);
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
