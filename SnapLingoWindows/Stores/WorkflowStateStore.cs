using SnapLingoWindows.Infrastructure;

namespace SnapLingoWindows.Stores;

public sealed class WorkflowStateStore : BindableBase
{
    private WorkflowPhase phase = WorkflowPhase.Idle;
    private TranslationMode selectedMode = TranslationMode.Translate;
    private string modeSourceLabel = "Auto";
    private string primaryTitle = "Ready";
    private string? primaryText;
    private string? originalPreview;
    private string? secondaryStatus;
    private bool canCopy;
    private bool isCopied;
    private bool canRetry;

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
        Phase == WorkflowPhase.LoadingPolish;

    public void ResetForNewSession()
    {
        Phase = WorkflowPhase.Capturing;
        PrimaryTitle = "Capturing Selection";
        PrimaryText = null;
        OriginalPreview = null;
        SecondaryStatus = "Waiting for selected text or a clipboard fallback.";
        CanCopy = false;
        CanRetry = false;
        IsCopied = false;
    }

    public void ResetForIdle()
    {
        Phase = WorkflowPhase.Idle;
        PrimaryTitle = "Ready";
        PrimaryText = null;
        OriginalPreview = null;
        SecondaryStatus = "Use the global hotkey or trigger the workflow from this window.";
        CanCopy = false;
        CanRetry = false;
        IsCopied = false;
        ModeSourceLabel = "Auto";
    }

    public void PresentClipboardFallback()
    {
        Phase = WorkflowPhase.WaitingForClipboard;
        PrimaryTitle = "Press Copy To Continue";
        PrimaryText = "Windows preview currently uses clipboard fallback for selection capture.";
        SecondaryStatus = "Press Ctrl+C in the current app. SnapLingo will continue automatically as soon as the clipboard changes.";
        CanCopy = false;
        CanRetry = true;
    }

    public void BeginProcessing(string text, TranslationMode mode, string sourceLabel)
    {
        SelectedMode = mode;
        ModeSourceLabel = sourceLabel;
        OriginalPreview = text.ReplaceLineEndings(" ").Trim();
        CanRetry = true;
        IsCopied = false;

        if (mode == TranslationMode.Translate)
        {
            Phase = WorkflowPhase.LoadingTranslation;
            PrimaryTitle = "Quick Translation";
            PrimaryText = null;
            SecondaryStatus = "Generating a send-ready translation.";
            CanCopy = false;
            return;
        }

        Phase = WorkflowPhase.LoadingPolish;
        PrimaryTitle = "Polished Version";
        PrimaryText = null;
        SecondaryStatus = "Polishing your English.";
        CanCopy = false;
    }

    public void ShowPartialTranslation(string text)
    {
        Phase = WorkflowPhase.Partial;
        PrimaryTitle = "Quick Translation";
        PrimaryText = text;
        SecondaryStatus = "Optimizing the result.";
        CanCopy = true;
    }

    public void ShowFinalResult(string title, string text)
    {
        Phase = WorkflowPhase.Ready;
        PrimaryTitle = title;
        PrimaryText = text;
        SecondaryStatus = null;
        CanCopy = true;
    }

    public void ShowError(string message)
    {
        Phase = WorkflowPhase.Error;
        PrimaryTitle = "Could Not Finish";
        PrimaryText = message;
        SecondaryStatus = "Retry the flow or switch modes.";
        CanCopy = false;
        CanRetry = true;
    }

    public void ShowCopiedFeedback()
    {
        IsCopied = true;
        SecondaryStatus = "Copied";
    }
}
