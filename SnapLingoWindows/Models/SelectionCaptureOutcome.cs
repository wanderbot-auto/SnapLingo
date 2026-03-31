namespace SnapLingoWindows.Models;

public abstract record SelectionCaptureOutcome
{
    public sealed record Text(string Value) : SelectionCaptureOutcome;
    public sealed record RequiresClipboardFallback(uint ChangeCount) : SelectionCaptureOutcome;
}
