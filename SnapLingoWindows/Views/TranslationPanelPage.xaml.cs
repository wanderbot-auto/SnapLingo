using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
            nameof(WorkflowPanelViewModel.IsPolishModeSelected))
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
            TranslateModeImage,
            ViewModel.WorkflowPanel.IsTranslateModeSelected,
            selectedBackground,
            unselectedBackground,
            selectedBorder,
            unselectedBorder,
            selectedImageKey: "TranslateModeActiveIcon",
            unselectedImageKey: "TranslateModeInactiveIcon");

        ApplyModeState(
            PolishModeButton,
            PolishModeImage,
            ViewModel.WorkflowPanel.IsPolishModeSelected,
            selectedBackground,
            unselectedBackground,
            selectedBorder,
            unselectedBorder,
            selectedImageKey: "PolishModeActiveIcon",
            unselectedImageKey: "PolishModeInactiveIcon");
    }

    private void ApplyModeState(
        Button button,
        Image image,
        bool isSelected,
        Brush selectedBackground,
        Brush unselectedBackground,
        Brush selectedBorder,
        Brush unselectedBorder,
        string selectedImageKey,
        string unselectedImageKey)
    {
        button.Background = isSelected ? selectedBackground : unselectedBackground;
        button.BorderBrush = isSelected ? selectedBorder : unselectedBorder;
        image.Source = LookupImageSource(isSelected ? selectedImageKey : unselectedImageKey);
    }

    private void QueueHeightUpdate()
    {
        DispatcherQueue.TryEnqueue(() => requestPanelHeightUpdate(MeasurePreferredHeight()));
    }

    private SolidColorBrush LookupBrush(string key)
    {
        return (SolidColorBrush)Resources[key];
    }

    private ImageSource LookupImageSource(string key)
    {
        return (ImageSource)Resources[key];
    }

    private async void OnTranslateModeClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.SelectModeAsync(TranslationMode.Translate);
    }

    private async void OnPolishModeClicked(object sender, RoutedEventArgs e)
    {
        await ViewModel.SelectModeAsync(TranslationMode.Polish);
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
