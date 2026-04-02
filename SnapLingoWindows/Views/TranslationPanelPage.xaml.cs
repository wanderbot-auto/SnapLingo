using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SnapLingoWindows.ViewModels;
using Windows.Foundation;

namespace SnapLingoWindows.Views;

public sealed partial class TranslationPanelPage : Page
{
    private readonly Action<double> requestPanelHeightUpdate;

    public TranslationPanelPage(MainViewModel viewModel, Action<double> requestPanelHeightUpdate)
    {
        ViewModel = viewModel;
        this.requestPanelHeightUpdate = requestPanelHeightUpdate;
        InitializeComponent();
        ViewModel.WorkflowPanel.PropertyChanged += OnWorkflowPanelChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnPageSizeChanged;
        ApplyModeVisualState();
    }

    public MainViewModel ViewModel { get; }

    public FrameworkElement TitleBarElement => HeaderDragRegion;

    public double MeasurePreferredHeight()
    {
        var measureWidth = PanelCardBorder.ActualWidth > 0 ? PanelCardBorder.ActualWidth : 520;
        PanelCardBorder.Measure(new Size(measureWidth, double.PositiveInfinity));
        return PanelCardBorder.DesiredSize.Height + 24;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        QueueHeightUpdate();
    }

    private void OnWorkflowPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkflowPanelViewModel.IsTranslateModeSelected) or
            nameof(WorkflowPanelViewModel.IsPolishModeSelected) or
            nameof(WorkflowPanelViewModel.IsContinueModeSelected))
        {
            ApplyModeVisualState();
        }

        if (e.PropertyName is nameof(WorkflowPanelViewModel.AnimatedPrimaryText) or
            nameof(WorkflowPanelViewModel.OriginalPreviewText) or
            nameof(WorkflowPanelViewModel.PlaceholderVisibility) or
            nameof(WorkflowPanelViewModel.SecondaryStatusVisibility) or
            nameof(WorkflowPanelViewModel.StatusBannerVisibility) or
            nameof(WorkflowPanelViewModel.ProcessedTitleText))
        {
            QueueHeightUpdate();
        }
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueHeightUpdate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.WorkflowPanel.PropertyChanged -= OnWorkflowPanelChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnPageSizeChanged;
    }

    private void ApplyModeVisualState()
    {
        var selectedBackground = LookupBrush("PanelSelectedModeBrush");
        var unselectedBackground = LookupBrush("PanelTransparentBrush");
        var selectedBorder = LookupBrush("PanelSelectedBorderBrush");
        var unselectedBorder = LookupBrush("PanelTransparentBrush");

        ApplyModeState(
            TranslateModeButton,
            TranslateModeTextBlock,
            ViewModel.WorkflowPanel.IsTranslateModeSelected,
            selectedBackground,
            unselectedBackground,
            selectedBorder,
            unselectedBorder);

        ApplyModeState(
            PolishModeButton,
            PolishModeTextBlock,
            ViewModel.WorkflowPanel.IsPolishModeSelected,
            selectedBackground,
            unselectedBackground,
            selectedBorder,
            unselectedBorder);

        ApplyModeState(
            ContinueModeButton,
            ContinueModeTextBlock,
            ViewModel.WorkflowPanel.IsContinueModeSelected,
            selectedBackground,
            unselectedBackground,
            selectedBorder,
            unselectedBorder);
    }

    private void ApplyModeState(
        Button button,
        TextBlock label,
        bool isSelected,
        Brush selectedBackground,
        Brush unselectedBackground,
        Brush selectedBorder,
        Brush unselectedBorder)
    {
        button.Background = isSelected ? selectedBackground : unselectedBackground;
        button.BorderBrush = isSelected ? selectedBorder : unselectedBorder;
        label.Foreground = isSelected
            ? LookupBrush("PanelSecondaryTextBrush")
            : LookupBrush("PanelMutedTextBrush");
    }

    private void QueueHeightUpdate()
    {
        DispatcherQueue.TryEnqueue(() => requestPanelHeightUpdate(MeasurePreferredHeight()));
    }

    private SolidColorBrush LookupBrush(string key)
    {
        return (SolidColorBrush)Resources[key];
    }

    private async void OnTranslateModeClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.SelectModeAsync(TranslationMode.Translate);
    }

    private async void OnPolishModeClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.SelectModeAsync(TranslationMode.Polish);
    }

    private async void OnContinueModeClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.SelectModeAsync(TranslationMode.Continue);
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        (Application.Current as App)?.HideTranslationPanel();
    }

    private async void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.CopyPrimaryResultAsync();
    }

    private async void OnRetryClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.RetryCurrentFlowAsync();
    }

    private async void OnStatusBannerButtonClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.RetryCurrentFlowAsync();
    }
}
