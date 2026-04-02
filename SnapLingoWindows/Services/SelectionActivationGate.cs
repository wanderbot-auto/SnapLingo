using SnapLingoWindows.Models;

namespace SnapLingoWindows.Services;

public readonly record struct SelectionActivationSettings(TimeSpan DebounceWindow, int MinimumWordCount)
{
    public const int DefaultDebounceMilliseconds = 280;
    public const int DefaultMinimumWordCount = 4;

    public static SelectionActivationSettings Default =>
        new(TimeSpan.FromMilliseconds(DefaultDebounceMilliseconds), DefaultMinimumWordCount);

    public static SelectionActivationSettings Create(int debounceMilliseconds, int minimumWordCount)
    {
        var sanitizedDebounceMilliseconds = Math.Clamp(debounceMilliseconds, 80, 2000);
        var sanitizedMinimumWordCount = Math.Clamp(minimumWordCount, 1, 12);
        return new(
            TimeSpan.FromMilliseconds(sanitizedDebounceMilliseconds),
            sanitizedMinimumWordCount);
    }
}

public sealed class SelectionActivationGate
{
    private readonly TimeSpan duplicateSuppressWindow;
    private readonly TimeSpan debounceWindow;
    private readonly int minimumWordCount;
    private string? lastTriggeredText;
    private DateTimeOffset lastTriggeredAt = DateTimeOffset.MinValue;
    private string? pendingText;
    private DateTimeOffset pendingObservedAt = DateTimeOffset.MinValue;

    public SelectionActivationGate(
        TimeSpan? duplicateSuppressWindow = null,
        TimeSpan? debounceWindow = null,
        int minimumWordCount = 4)
    {
        this.duplicateSuppressWindow = duplicateSuppressWindow ?? TimeSpan.FromSeconds(6);
        this.debounceWindow = debounceWindow ?? TimeSpan.FromMilliseconds(280);
        this.minimumWordCount = minimumWordCount;
    }

    public SelectionActivationRequest? TryCreateRequest(
        SelectionSnapshot? selection,
        int fallbackAnchorX,
        int fallbackAnchorY,
        DateTimeOffset now)
    {
        var text = selection?.Text?.Trim();
        if (!HasMinimumWordCount(text))
        {
            ClearPendingCandidate();
            return null;
        }

        if (ShouldSuppress(text!, now))
        {
            ClearPendingCandidate();
            return null;
        }

        if (!string.Equals(text, pendingText, StringComparison.Ordinal))
        {
            pendingText = text;
            pendingObservedAt = now;
            return null;
        }

        if (now - pendingObservedAt < debounceWindow)
        {
            return null;
        }

        ClearPendingCandidate();
        lastTriggeredText = text;
        lastTriggeredAt = now;

        return new SelectionActivationRequest(
            text,
            selection?.AnchorX ?? fallbackAnchorX,
            selection?.AnchorY ?? fallbackAnchorY);
    }

    public bool HasPendingCandidate(string? text)
    {
        var normalizedText = text?.Trim();
        return HasMinimumWordCount(normalizedText) &&
               string.Equals(normalizedText, pendingText, StringComparison.Ordinal);
    }

    public bool ShouldSuppress(string text, DateTimeOffset now)
    {
        return !string.IsNullOrWhiteSpace(lastTriggeredText) &&
               string.Equals(text, lastTriggeredText, StringComparison.Ordinal) &&
               now - lastTriggeredAt < duplicateSuppressWindow;
    }

    private bool HasMinimumWordCount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length >= minimumWordCount;
    }

    private void ClearPendingCandidate()
    {
        pendingText = null;
        pendingObservedAt = DateTimeOffset.MinValue;
    }
}
