namespace SnapLingoWindows.Models;

public enum WorkflowPhase
{
    Idle,
    Capturing,
    WaitingForClipboard,
    LoadingTranslation,
    Partial,
    LoadingPolish,
    LoadingContinue,
    Ready,
    Error,
}
