using SnapLingoWindows.Infrastructure;

namespace SnapLingoWindows.Stores;

public sealed class WorkflowStateStore : BindableBase
{
    private enum WorkflowTextState
    {
        Idle,
        Capturing,
        ClipboardFallback,
        Translating,
        PartialTranslation,
        Polishing,
        Continuing,
        Ready,
        Error,
    }

    private readonly LocalizationService localizer;
    private WorkflowPhase phase = WorkflowPhase.Idle;
    private TranslationMode selectedMode = TranslationMode.Translate;
    private string modeSourceLabel = string.Empty;
    private string modeSourceKey = "source_auto";
    private string primaryTitle = string.Empty;
    private string? primaryText;
    private string? originalPreview;
    private string? secondaryStatus;
    private bool canCopy;
    private bool isCopied;
    private bool canRetry;
    private WorkflowTextState textState = WorkflowTextState.Idle;

    public WorkflowStateStore(LocalizationService localizer)
    {
        this.localizer = localizer;
        this.localizer.LanguageChanged += OnLanguageChanged;
        ApplyLocalizedState();
    }

    public WorkflowPhase Phase
    {
        get => phase;
        private set
        {
            if (SetProperty(ref phase, value))
            {
                OnPropertyChanged(nameof(IsBusy));
            }
        }
    }

    public TranslationMode SelectedMode
    {
        get => selectedMode;
        set => SetProperty(ref selectedMode, value);
    }

    public string ModeSourceLabel
    {
        get => modeSourceLabel;
        private set => SetProperty(ref modeSourceLabel, value);
    }

    public string PrimaryTitle
    {
        get => primaryTitle;
        private set => SetProperty(ref primaryTitle, value);
    }

    public string? PrimaryText
    {
        get => primaryText;
        private set => SetProperty(ref primaryText, value);
    }

    public string? OriginalPreview
    {
        get => originalPreview;
        private set => SetProperty(ref originalPreview, value);
    }

    public string? SecondaryStatus
    {
        get => secondaryStatus;
        private set => SetProperty(ref secondaryStatus, value);
    }

    public bool CanCopy
    {
        get => canCopy;
        private set => SetProperty(ref canCopy, value);
    }

    public bool IsCopied
    {
        get => isCopied;
        private set => SetProperty(ref isCopied, value);
    }

    public bool CanRetry
    {
        get => canRetry;
        private set => SetProperty(ref canRetry, value);
    }

    public bool IsBusy =>
        Phase == WorkflowPhase.Capturing ||
        Phase == WorkflowPhase.LoadingTranslation ||
        Phase == WorkflowPhase.LoadingPolish ||
        Phase == WorkflowPhase.LoadingContinue;

    public void ResetForNewSession()
    {
        Phase = WorkflowPhase.Capturing;
        textState = WorkflowTextState.Capturing;
        PrimaryText = null;
        OriginalPreview = null;
        CanCopy = false;
        CanRetry = false;
        IsCopied = false;
        ApplyLocalizedState();
    }

    public void ResetForIdle()
    {
        Phase = WorkflowPhase.Idle;
        textState = WorkflowTextState.Idle;
        PrimaryText = null;
        OriginalPreview = null;
        CanCopy = false;
        CanRetry = false;
        IsCopied = false;
        modeSourceKey = "source_auto";
        ApplyLocalizedState();
    }

    public void PresentClipboardFallback()
    {
        Phase = WorkflowPhase.WaitingForClipboard;
        textState = WorkflowTextState.ClipboardFallback;
        PrimaryText = localizer.Get("state_clipboard_primary");
        CanCopy = false;
        CanRetry = true;
        ApplyLocalizedState();
    }

    public void BeginProcessing(string text, TranslationMode mode, string sourceLabelKey)
    {
        SelectedMode = mode;
        modeSourceKey = sourceLabelKey;
        OriginalPreview = text.ReplaceLineEndings(" ").Trim();
        CanRetry = true;
        IsCopied = false;

        if (mode == TranslationMode.Translate)
        {
            Phase = WorkflowPhase.LoadingTranslation;
            textState = WorkflowTextState.Translating;
            PrimaryText = null;
            CanCopy = false;
            ApplyLocalizedState();
            return;
        }

        if (mode == TranslationMode.Continue)
        {
            Phase = WorkflowPhase.LoadingContinue;
            textState = WorkflowTextState.Continuing;
            PrimaryText = null;
            CanCopy = false;
            ApplyLocalizedState();
            return;
        }

        Phase = WorkflowPhase.LoadingPolish;
        textState = WorkflowTextState.Polishing;
        PrimaryText = null;
        CanCopy = false;
        ApplyLocalizedState();
    }

    public void ShowPartialTranslation(string text)
    {
        Phase = WorkflowPhase.Partial;
        textState = WorkflowTextState.PartialTranslation;
        PrimaryText = text;
        CanCopy = true;
        ApplyLocalizedState();
    }

    public void ShowPartialTranslationFallback(string status)
    {
        Phase = WorkflowPhase.Partial;
        textState = WorkflowTextState.PartialTranslation;
        PrimaryTitle = localizer.Get("state_translate_title");
        SecondaryStatus = status;
        CanCopy = true;
        CanRetry = true;
    }

    public void ShowFinalResult(string text)
    {
        Phase = WorkflowPhase.Ready;
        textState = WorkflowTextState.Ready;
        PrimaryText = text;
        CanCopy = true;
        ApplyLocalizedState();
    }

    public void ShowError(string message)
    {
        Phase = WorkflowPhase.Error;
        textState = WorkflowTextState.Error;
        PrimaryText = message;
        CanCopy = false;
        CanRetry = true;
        ApplyLocalizedState();
    }

    public void ShowCopiedFeedback()
    {
        IsCopied = true;
        SecondaryStatus = localizer.Get("state_copied_status");
    }

    private void ApplyLocalizedState()
    {
        ModeSourceLabel = localizer.Get(modeSourceKey);

        switch (textState)
        {
            case WorkflowTextState.Idle:
                PrimaryTitle = localizer.Get("state_ready_title");
                SecondaryStatus = localizer.Get("state_ready_status");
                break;
            case WorkflowTextState.Capturing:
                PrimaryTitle = localizer.Get("state_capturing_title");
                SecondaryStatus = localizer.Get("state_capturing_status");
                break;
            case WorkflowTextState.ClipboardFallback:
                PrimaryTitle = localizer.Get("state_clipboard_title");
                SecondaryStatus = localizer.Get("state_clipboard_status");
                PrimaryText = localizer.Get("state_clipboard_primary");
                break;
            case WorkflowTextState.Translating:
                PrimaryTitle = localizer.Get("state_translate_title");
                SecondaryStatus = localizer.Get("state_translate_status");
                break;
            case WorkflowTextState.PartialTranslation:
                PrimaryTitle = localizer.Get("state_translate_title");
                SecondaryStatus = localizer.Get("state_partial_status");
                break;
            case WorkflowTextState.Polishing:
                PrimaryTitle = localizer.Get("state_polish_title");
                SecondaryStatus = localizer.Get("state_polish_status");
                break;
            case WorkflowTextState.Continuing:
                PrimaryTitle = localizer.Get("state_continue_title");
                SecondaryStatus = localizer.Get("state_continue_status");
                break;
            case WorkflowTextState.Ready:
                PrimaryTitle = SelectedMode switch
                {
                    TranslationMode.Translate => localizer.Get("state_translate_title"),
                    TranslationMode.Polish => localizer.Get("state_polish_title"),
                    TranslationMode.Continue => localizer.Get("state_continue_title"),
                    _ => localizer.Get("state_ready_title"),
                };
                SecondaryStatus = IsCopied ? localizer.Get("state_copied_status") : null;
                break;
            case WorkflowTextState.Error:
                PrimaryTitle = localizer.Get("state_error_title");
                SecondaryStatus = localizer.Get("state_error_status");
                break;
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ApplyLocalizedState();
    }
}
