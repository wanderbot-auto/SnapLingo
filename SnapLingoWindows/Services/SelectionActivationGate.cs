using SnapLingoWindows.Models;

namespace SnapLingoWindows.Services;

public sealed class SelectionActivationGate
{
    private readonly TimeSpan duplicateSuppressWindow;
    private string? lastTriggeredText;
    private DateTimeOffset lastTriggeredAt = DateTimeOffset.MinValue;

    public SelectionActivationGate(TimeSpan? duplicateSuppressWindow = null)
    {
        this.duplicateSuppressWindow = duplicateSuppressWindow ?? TimeSpan.FromSeconds(6);
    }

    public SelectionActivationRequest? TryCreateRequest(
        SelectionSnapshot? selection,
        int fallbackAnchorX,
        int fallbackAnchorY,
        DateTimeOffset now)
    {
        var text = selection?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || ShouldSuppress(text, now))
        {
            return null;
        }

        lastTriggeredText = text;
        lastTriggeredAt = now;

        return new SelectionActivationRequest(
            text,
            selection?.AnchorX ?? fallbackAnchorX,
            selection?.AnchorY ?? fallbackAnchorY);
    }

    public bool ShouldSuppress(string text, DateTimeOffset now)
    {
        return !string.IsNullOrWhiteSpace(lastTriggeredText) &&
               string.Equals(text, lastTriggeredText, StringComparison.Ordinal) &&
               now - lastTriggeredAt < duplicateSuppressWindow;
    }
}
