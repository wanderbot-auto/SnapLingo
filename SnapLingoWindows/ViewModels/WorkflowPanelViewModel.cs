using Microsoft.UI.Xaml;
using SnapLingoWindows.Infrastructure;
using SnapLingoWindows.Services;
using SnapLingoWindows.Stores;
using Windows.ApplicationModel.DataTransfer;

namespace SnapLingoWindows.ViewModels;

public sealed class WorkflowPanelViewModel : BindableBase
{
    private readonly LocalizationService localizer;
    private readonly WorkflowOrchestrator orchestrator;
    private readonly Func<string> currentModelAccessor;
    private readonly Func<AppLanguage> currentLanguageAccessor;
    private CancellationTokenSource? animationCts;
    private string animationTargetText = string.Empty;
    private WorkflowPhase animationPhase = WorkflowPhase.Idle;
    private string animatedPrimaryText = string.Empty;

    public WorkflowPanelViewModel(
        LocalizationService localizer,
        ISelectionCaptureService selectionCaptureService,
        IProviderClientFactory providerClientFactory,
        Func<string> currentModelAccessor,
        Func<AppLanguage> currentLanguageAccessor)
    {
        this.localizer = localizer;
        this.currentModelAccessor = currentModelAccessor;
        this.currentLanguageAccessor = currentLanguageAccessor;

        Workflow = new WorkflowStateStore(localizer);
        Workflow.ResetForIdle();
        Workflow.PropertyChanged += OnWorkflowPropertyChanged;
        localizer.LanguageChanged += OnLanguageChanged;

        orchestrator = new WorkflowOrchestrator(
            Workflow,
            selectionCaptureService,
            providerClientFactory,
            localizer,
            () => ShowRequested?.Invoke(this, EventArgs.Empty));

        RefreshPresentationState();
    }

    public event EventHandler? ShowRequested;
    public event EventHandler? HideRequested;

    public WorkflowStateStore Workflow { get; }

    public string CurrentModelId => currentModelAccessor();

    public string CurrentModelDisplayName => FormatModelDisplayName(CurrentModelId);

    public AppLanguage SelectedLanguage => currentLanguageAccessor();

    public string WorkflowStatusText => Workflow.Phase == WorkflowPhase.Idle
        ? localizer.Get("workflow_ready")
        : localizer.Get("workflow_active");

    public string OriginalSectionLabel => localizer.Get("panel_original_label");

    public string SourceLanguageLabel => localizer.Get("panel_source_language_auto");

    public string TargetLanguageLabel => localizer.Get("panel_target_language_english");

    public string OriginalPreviewText => !string.IsNullOrWhiteSpace(Workflow.OriginalPreview)
        ? Workflow.OriginalPreview!
        : ResolveOriginalContentHint();

    public string ProcessedTitleText => Workflow.PrimaryTitle;

    public Visibility PlaceholderVisibility => ShouldShowPlaceholder ? Visibility.Visible : Visibility.Collapsed;

    public bool IsBusy => Workflow.IsBusy;

    public Visibility BusyProgressVisibility => Workflow.IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public string PlaceholderText => ResolvePlaceholderText();

    public Visibility PrimaryTextVisibility => ShouldShowPlaceholder ? Visibility.Collapsed : Visibility.Visible;

    public string AnimatedPrimaryText
    {
        get => animatedPrimaryText;
        private set => SetProperty(ref animatedPrimaryText, value);
    }

    public string SecondaryStatusText => ResolveSecondaryStatusText(ShouldShowPlaceholder);

    public Visibility SecondaryStatusVisibility =>
        string.IsNullOrWhiteSpace(SecondaryStatusText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility StatusBannerVisibility => HasStatusBanner ? Visibility.Visible : Visibility.Collapsed;

    public string StatusBannerText => Workflow.Phase switch
    {
        WorkflowPhase.WaitingForClipboard => localizer.Get("panel_clipboard_banner"),
        WorkflowPhase.Error when Workflow.CanRetry => localizer.Get("panel_error_banner"),
        _ => string.Empty,
    };

    public string StatusBannerButtonText => localizer.Get("button_retry");

    public bool CanRetry => Workflow.CanRetry;

    public double RetryButtonOpacity => Workflow.CanRetry ? 1 : 0.45;

    public bool CanCopy => Workflow.CanCopy;

    public double CopyButtonOpacity => Workflow.CanCopy ? 1 : 0.72;

    public string CopyGlyph => Workflow.IsCopied ? "\uE73E" : "\uE8C8";

    public bool IsTranslateModeSelected => Workflow.SelectedMode == TranslationMode.Translate;

    public bool IsPolishModeSelected => Workflow.SelectedMode == TranslationMode.Polish;

    private bool ShouldShowPlaceholder =>
        string.IsNullOrWhiteSpace(Workflow.PrimaryText) || Workflow.IsBusy;

    private bool HasStatusBanner =>
        Workflow.Phase == WorkflowPhase.WaitingForClipboard ||
        (Workflow.Phase == WorkflowPhase.Error && Workflow.CanRetry);

    public async Task HandleHotkeyAsync()
    {
        await orchestrator.HandleHotkeyAsync();
    }

    public async Task HandleCapturedSelectionAsync(string text)
    {
        await orchestrator.HandleCapturedSelectionAsync(text);
    }

    public async Task HandleSelectionLauncherAsync(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            await orchestrator.HandleCapturedSelectionAsync(text);
            return;
        }

        await orchestrator.HandleHotkeyAsync();
    }

    public async Task RetryCurrentFlowAsync()
    {
        await orchestrator.RetryAsync();
    }

    public async Task UseCurrentClipboardAsync()
    {
        await orchestrator.UseCurrentClipboardAsync();
    }

    public async Task SelectModeAsync(TranslationMode mode)
    {
        await orchestrator.SwitchModeAsync(mode);
    }

    public async Task CopyPrimaryResultAsync()
    {
        if (!Workflow.CanCopy || string.IsNullOrWhiteSpace(Workflow.PrimaryText))
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(Workflow.PrimaryText);
        Clipboard.SetContent(package);
        Workflow.ShowCopiedFeedback();

        await Task.Delay(TimeSpan.FromMilliseconds(350));
        Workflow.ResetForIdle();
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyProviderSelectionChanged()
    {
        OnPropertyChanged(nameof(CurrentModelId));
        OnPropertyChanged(nameof(CurrentModelDisplayName));
    }

    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(SelectedLanguage));
        RefreshLocalizedPresentation();
    }

    private void OnWorkflowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkflowStateStore.Phase))
        {
            OnPropertyChanged(nameof(WorkflowStatusText));
        }

        RefreshPresentationState();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        NotifyLanguageChanged();
    }

    private void RefreshPresentationState()
    {
        RefreshAnimatedPrimaryText();

        OnPropertyChanged(nameof(OriginalPreviewText));
        OnPropertyChanged(nameof(ProcessedTitleText));
        OnPropertyChanged(nameof(PlaceholderVisibility));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(BusyProgressVisibility));
        OnPropertyChanged(nameof(PlaceholderText));
        OnPropertyChanged(nameof(PrimaryTextVisibility));
        OnPropertyChanged(nameof(SecondaryStatusText));
        OnPropertyChanged(nameof(SecondaryStatusVisibility));
        OnPropertyChanged(nameof(StatusBannerVisibility));
        OnPropertyChanged(nameof(StatusBannerText));
        OnPropertyChanged(nameof(CanRetry));
        OnPropertyChanged(nameof(RetryButtonOpacity));
        OnPropertyChanged(nameof(CanCopy));
        OnPropertyChanged(nameof(CopyButtonOpacity));
        OnPropertyChanged(nameof(CopyGlyph));
        OnPropertyChanged(nameof(IsTranslateModeSelected));
        OnPropertyChanged(nameof(IsPolishModeSelected));
    }

    private void RefreshLocalizedPresentation()
    {
        OnPropertyChanged(nameof(WorkflowStatusText));
        OnPropertyChanged(nameof(OriginalSectionLabel));
        OnPropertyChanged(nameof(SourceLanguageLabel));
        OnPropertyChanged(nameof(TargetLanguageLabel));
        OnPropertyChanged(nameof(StatusBannerButtonText));
        RefreshPresentationState();
    }

    private void RefreshAnimatedPrimaryText()
    {
        var rawText = Workflow.PrimaryText ?? string.Empty;

        if (ShouldShowPlaceholder)
        {
            CancelAnimation();
            AnimatedPrimaryText = string.Empty;
            return;
        }

        if (!ShouldAnimate(rawText))
        {
            CancelAnimation();
            animationTargetText = rawText;
            animationPhase = Workflow.Phase;
            AnimatedPrimaryText = rawText;
            return;
        }

        if (rawText == animationTargetText && animationPhase == Workflow.Phase)
        {
            return;
        }

        StartAnimation(rawText, Workflow.Phase);
    }

    private bool ShouldAnimate(string text)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               Workflow.Phase is WorkflowPhase.Partial or WorkflowPhase.Ready;
    }

    private void StartAnimation(string text, WorkflowPhase phase)
    {
        CancelAnimation();
        animationCts = new CancellationTokenSource();
        animationTargetText = text;
        animationPhase = phase;
        AnimatedPrimaryText = string.Empty;
        _ = AnimatePrimaryTextAsync(text, animationCts.Token);
    }

    private async Task AnimatePrimaryTextAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var chunkSize = text.Length switch
            {
                > 640 => 14,
                > 320 => 10,
                > 160 => 6,
                _ => 3,
            };

            var delay = text.Length > 240 ? 10 : 16;

            for (var index = chunkSize; index < text.Length + chunkSize; index += chunkSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AnimatedPrimaryText = text[..Math.Min(index, text.Length)];
                await Task.Delay(delay, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelAnimation()
    {
        animationCts?.Cancel();
        animationCts?.Dispose();
        animationCts = null;
    }

    private string ResolveOriginalContentHint()
    {
        return Workflow.Phase switch
        {
            WorkflowPhase.Capturing => localizer.Get("panel_original_hint_capturing"),
            WorkflowPhase.WaitingForClipboard => localizer.Get("panel_original_hint_waiting_clipboard"),
            WorkflowPhase.Error => localizer.Get("panel_original_hint_error"),
            _ => localizer.Get("panel_original_hint_idle"),
        };
    }

    private string ResolvePlaceholderText()
    {
        return Workflow.Phase switch
        {
            WorkflowPhase.Capturing => localizer.Get("panel_placeholder_capturing"),
            WorkflowPhase.LoadingTranslation => localizer.Get("panel_placeholder_loading_translation"),
            WorkflowPhase.LoadingPolish => localizer.Get("panel_placeholder_loading_polish"),
            WorkflowPhase.WaitingForClipboard => localizer.Get("panel_placeholder_waiting_clipboard"),
            WorkflowPhase.Partial => localizer.Get("panel_placeholder_partial"),
            WorkflowPhase.Error => Workflow.PrimaryText ?? localizer.Get("panel_placeholder_error"),
            _ => localizer.Get("panel_placeholder_idle"),
        };
    }

    private string ResolveSecondaryStatusText(bool showingPlaceholder)
    {
        if (showingPlaceholder || string.IsNullOrWhiteSpace(Workflow.SecondaryStatus))
        {
            return string.Empty;
        }

        if (Workflow.Phase == WorkflowPhase.Ready && !Workflow.IsCopied)
        {
            return string.Empty;
        }

        return Workflow.SecondaryStatus!;
    }

    private string FormatModelDisplayName(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return localizer.Get("panel_current_model");
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
}
