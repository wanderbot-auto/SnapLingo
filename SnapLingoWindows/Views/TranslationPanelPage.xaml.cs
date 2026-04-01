using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
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
        ViewModel.Workflow.PropertyChanged += OnWorkflowChanged;
        ViewModel.PropertyChanged += OnViewModelChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnPageSizeChanged;
        Render();
    }

    public MainViewModel ViewModel { get; }
    public FrameworkElement TitleBarElement => HeaderDragRegion;

    private bool IsChinese => ViewModel.SelectedLanguage == AppLanguage.Chinese;

    public double MeasurePreferredHeight()
    {
        var measureWidth = PanelCardBorder.ActualWidth > 0 ? PanelCardBorder.ActualWidth : 500;
        PanelCardBorder.Measure(new Size(measureWidth, double.PositiveInfinity));
        return PanelCardBorder.DesiredSize.Height + 32;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        QueueHeightUpdate();
    }

    private void OnWorkflowChanged(object? sender, PropertyChangedEventArgs e)
    {
        Render();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        Render();
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueHeightUpdate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Workflow.PropertyChanged -= OnWorkflowChanged;
        ViewModel.PropertyChanged -= OnViewModelChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnPageSizeChanged;
    }

    private void Render()
    {
        OriginalSectionLabelTextBlock.Text = IsChinese ? "原文" : "Original";
        TranslateModeLabelTextBlock.Text = ResolveModeLabel(TranslationMode.Translate);
        PolishModeLabelTextBlock.Text = ResolveModeLabel(TranslationMode.Polish);
        RetryButtonTextBlock.Text = ResolveRetryLabel();
        CopyButtonTextBlock.Text = ResolveCopyLabel(ViewModel.Workflow.IsCopied);
        StatusBannerButtonTextBlock.Text = ResolveRetryLabel();
        ActiveModelTextBlock.Text = FormatModelDisplayName(ViewModel.SelectedModelId);

        UpdateHeader();
        UpdateOriginalContent();
        UpdateModeVisualState();
        UpdateContent();
        UpdateControls();
        QueueHeightUpdate();
    }

    private void UpdateHeader()
    {
        SourceLanguageTextBlock.Text = ResolveSourceLanguageLabel();
        TargetLanguageTextBlock.Text = ResolveTargetLanguageLabel();
    }

    private void UpdateOriginalContent()
    {
        OriginalPreviewTextBlock.Text = !string.IsNullOrWhiteSpace(ViewModel.Workflow.OriginalPreview)
            ? ViewModel.Workflow.OriginalPreview!
            : ResolveOriginalContentHint();
    }

    private void UpdateModeVisualState()
    {
        var isTranslate = ViewModel.Workflow.SelectedMode == TranslationMode.Translate;
        var selectedBackground = LookupBrush("PanelSelectedModeBrush");
        var unselectedBackground = LookupBrush("PanelTransparentBrush");
        var selectedBorder = LookupBrush("PanelSelectedBorderBrush");
        var unselectedBorder = LookupBrush("PanelTransparentBrush");
        var selectedForeground = LookupBrush("PanelAccentBrush");
        var unselectedForeground = LookupBrush("PanelMutedTextBrush");

        ApplyModeState(
            TranslateModeButton,
            TranslateModeLabelTextBlock,
            TranslateModeImage,
            isSelected: isTranslate,
            selectedBackground,
            unselectedBackground,
            selectedBorder,
            unselectedBorder,
            selectedForeground,
            unselectedForeground,
            selectedImageKey: "TranslateModeActiveIcon",
            unselectedImageKey: "TranslateModeInactiveIcon");

        ApplyModeState(
            PolishModeButton,
            PolishModeLabelTextBlock,
            PolishModeImage,
            isSelected: !isTranslate,
            selectedBackground,
            unselectedBackground,
            selectedBorder,
            unselectedBorder,
            selectedForeground,
            unselectedForeground,
            selectedImageKey: "PolishModeActiveIcon",
            unselectedImageKey: "PolishModeInactiveIcon");
    }

    private void ApplyModeState(
        Button button,
        TextBlock label,
        Image image,
        bool isSelected,
        Brush selectedBackground,
        Brush unselectedBackground,
        Brush selectedBorder,
        Brush unselectedBorder,
        Brush selectedForeground,
        Brush unselectedForeground,
        string selectedImageKey,
        string unselectedImageKey)
    {
        button.Background = isSelected ? selectedBackground : unselectedBackground;
        button.BorderBrush = isSelected ? selectedBorder : unselectedBorder;
        label.Foreground = isSelected ? selectedForeground : unselectedForeground;
        image.Source = LookupImageSource(isSelected ? selectedImageKey : unselectedImageKey);
    }

    private void UpdateContent()
    {
        var displayText = ViewModel.Workflow.PrimaryText ?? string.Empty;
        var shouldUsePlaceholder = string.IsNullOrWhiteSpace(displayText) || ViewModel.Workflow.IsBusy;

        ProcessedTitleTextBlock.Text = ViewModel.Workflow.PrimaryTitle;

        PlaceholderPanel.Visibility = shouldUsePlaceholder ? Visibility.Visible : Visibility.Collapsed;
        BusyProgressRing.IsActive = ViewModel.Workflow.IsBusy;
        BusyProgressRing.Visibility = ViewModel.Workflow.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderTextBlock.Text = ResolvePlaceholderText();

        PrimaryTextBlock.Visibility = shouldUsePlaceholder ? Visibility.Collapsed : Visibility.Visible;
        PrimaryTextBlock.Text = shouldUsePlaceholder ? string.Empty : displayText;

        var secondaryStatus = ResolveSecondaryStatusText(shouldUsePlaceholder);
        SecondaryStatusTextBlock.Text = secondaryStatus;
        SecondaryStatusTextBlock.Visibility = string.IsNullOrWhiteSpace(secondaryStatus)
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (ViewModel.Workflow.Phase == WorkflowPhase.WaitingForClipboard)
        {
            StatusBannerBorder.Visibility = Visibility.Visible;
            StatusBannerTextBlock.Text = ResolveClipboardBannerText();
            StatusBannerButton.Tag = "retry";
            return;
        }

        if (ViewModel.Workflow.Phase == WorkflowPhase.Error && ViewModel.Workflow.CanRetry)
        {
            StatusBannerBorder.Visibility = Visibility.Visible;
            StatusBannerTextBlock.Text = ResolveErrorBannerText();
            StatusBannerButton.Tag = "retry";
            return;
        }

        StatusBannerBorder.Visibility = Visibility.Collapsed;
        StatusBannerTextBlock.Text = string.Empty;
        StatusBannerButton.Tag = null;
    }

    private void UpdateControls()
    {
        RetryButton.IsEnabled = ViewModel.Workflow.CanRetry;
        RetryButton.Opacity = ViewModel.Workflow.CanRetry ? 1 : 0.45;

        CopyButton.IsEnabled = ViewModel.Workflow.CanCopy;
        CopyButton.Opacity = ViewModel.Workflow.CanCopy ? 1 : 0.72;
    }

    private string ResolveOriginalContentHint()
    {
        return ViewModel.Workflow.Phase switch
        {
            WorkflowPhase.Capturing => IsChinese
                ? "正在读取你刚刚选中的内容。"
                : "Reading the text you just selected.",
            WorkflowPhase.WaitingForClipboard => IsChinese
                ? "直接取词失败时，会回退到剪贴板继续处理。"
                : "If direct selection capture fails, SnapLingo will continue from the clipboard fallback.",
            WorkflowPhase.Error => IsChinese
                ? "这次流程没有完成。重新选择内容后会再次自动弹出。"
                : "This run did not finish. Select text again and the panel will reopen automatically.",
            _ => IsChinese
                ? "在 Windows 任意位置选中文本后，SnapLingo 会自动弹出这个独立面板。"
                : "Select text anywhere in Windows and SnapLingo will open this standalone panel automatically.",
        };
    }

    private string ResolvePlaceholderText()
    {
        return ViewModel.Workflow.Phase switch
        {
            WorkflowPhase.Capturing => IsChinese ? "正在捕获选中文本..." : "Capturing the selected text...",
            WorkflowPhase.LoadingTranslation => IsChinese ? "正在生成快速翻译..." : "Generating a quick translation...",
            WorkflowPhase.LoadingPolish => IsChinese ? "正在润色文本..." : "Polishing the text...",
            WorkflowPhase.WaitingForClipboard => IsChinese ? "等待剪贴板内容..." : "Waiting for clipboard content...",
            WorkflowPhase.Partial => IsChinese ? "正在生成最终润色结果..." : "Generating the final polished result...",
            WorkflowPhase.Error => ViewModel.Workflow.PrimaryText ?? (IsChinese ? "这次流程没有完成。" : "The workflow did not finish."),
            _ => IsChinese ? "等待下一次选中文本。" : "Ready for the next selection.",
        };
    }

    private string ResolveSecondaryStatusText(bool showingPlaceholder)
    {
        if (showingPlaceholder || string.IsNullOrWhiteSpace(ViewModel.Workflow.SecondaryStatus))
        {
            return string.Empty;
        }

        if (ViewModel.Workflow.Phase == WorkflowPhase.Ready && !ViewModel.Workflow.IsCopied)
        {
            return string.Empty;
        }

        return ViewModel.Workflow.SecondaryStatus!;
    }

    private string ResolveClipboardBannerText()
    {
        return IsChinese
            ? "当前应用不支持直接读取选区时，请按 Ctrl+C，SnapLingo 会继续处理。"
            : "If direct capture is not available in the current app, press Ctrl+C and SnapLingo will continue automatically.";
    }

    private string ResolveErrorBannerText()
    {
        return IsChinese
            ? "这次处理没有完成，可以重试，或重新选择文本。"
            : "The workflow did not finish. Retry it, or select the text again.";
    }

    private string ResolveModeLabel(TranslationMode mode)
    {
        return mode == TranslationMode.Translate
            ? (IsChinese ? "翻译" : "Translate")
            : (IsChinese ? "润色" : "Polish");
    }

    private string ResolveCopyLabel(bool copied)
    {
        if (copied)
        {
            return IsChinese ? "已复制" : "Copied";
        }

        return IsChinese ? "复制" : "Copy";
    }

    private string ResolveRetryLabel() => IsChinese ? "重试" : "Retry";

    private string ResolveSourceLanguageLabel() => IsChinese ? "自动检测" : "Auto-Detect";

    private string ResolveTargetLanguageLabel() => IsChinese ? "英文" : "English";

    private string FormatModelDisplayName(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return IsChinese ? "当前模型" : "Current model";
        }

        if (modelId.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase))
        {
            return $"GPT-{modelId[4..]}";
        }

        if (modelId.StartsWith("claude-", StringComparison.OrdinalIgnoreCase))
        {
            return $"Claude {modelId[7..]}";
        }

        return modelId;
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
        if (StatusBannerButton.Tag as string == "retry")
        {
            await ViewModel.RetryCurrentFlowAsync();
        }
    }
}
