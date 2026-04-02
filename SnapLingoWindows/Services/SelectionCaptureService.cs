using System.Windows.Automation;
using Windows.ApplicationModel.DataTransfer;
using SnapLingoWindows.Models;

namespace SnapLingoWindows.Services;

public sealed class ClipboardSelectionCaptureService : ISelectionCaptureService
{
    private static readonly TimeSpan DirectSelectionTimeout = TimeSpan.FromMilliseconds(120);

    public async Task<SelectionCaptureOutcome> CaptureSelectionAsync(CancellationToken cancellationToken)
    {
        var directSelection = await TryCaptureDirectSelectionWithTimeoutAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(directSelection?.Text))
        {
            return new SelectionCaptureOutcome.Text(directSelection.Text);
        }

        return new SelectionCaptureOutcome.RequiresClipboardFallback(NativeMethods.GetClipboardSequenceNumber());
    }

    public async Task<SelectionSnapshot?> TryCaptureSelectionSnapshotAsync(CancellationToken cancellationToken)
    {
        var directSelection = await TryCaptureDirectSelectionWithTimeoutAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(directSelection?.Text))
        {
            return new SelectionSnapshot(
                directSelection.Text.Trim(),
                directSelection.AnchorX,
                directSelection.AnchorY);
        }

        return null;
    }

    public async Task<string?> TryCaptureSelectionTextAsync(CancellationToken cancellationToken)
    {
        return (await TryCaptureSelectionSnapshotAsync(cancellationToken))?.Text;
    }

    public async Task<string?> WaitForClipboardChangeAsync(uint afterChangeCount, CancellationToken cancellationToken)
    {
        for (var index = 0; index < 75; index++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);

            if (NativeMethods.GetClipboardSequenceNumber() == afterChangeCount)
            {
                continue;
            }

            var text = await ReadClipboardTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }

        return null;
    }

    public async Task<string?> ReadClipboardTextAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Text))
            {
                return null;
            }

            var text = await content.GetTextAsync();
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static Task<SelectionSnapshot?> TryCaptureDirectSelectionAsync(CancellationToken cancellationToken)
    {
        return Task.Run<SelectionSnapshot?>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var focusedElement = AutomationElement.FocusedElement;
                if (focusedElement is null)
                {
                    return (SelectionSnapshot?)null;
                }

                if (focusedElement.Current.ProcessId == Environment.ProcessId)
                {
                    return (SelectionSnapshot?)null;
                }

                foreach (var element in EnumerateFocusableChain(focusedElement))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var selection = TryReadFromTextPattern(element);
                    if (!string.IsNullOrWhiteSpace(selection?.Text))
                    {
                        return selection;
                    }

                    selection = TryReadFromSelectionPattern(element);
                    if (!string.IsNullOrWhiteSpace(selection?.Text))
                    {
                        return selection;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }

            return (SelectionSnapshot?)null;
        }, cancellationToken);
    }

    private static async Task<SelectionSnapshot?> TryCaptureDirectSelectionWithTimeoutAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var directCaptureTask = TryCaptureDirectSelectionAsync(timeoutCts.Token);
        var completedTask = await Task.WhenAny(
            directCaptureTask,
            Task.Delay(DirectSelectionTimeout, cancellationToken));

        if (completedTask == directCaptureTask)
        {
            return await directCaptureTask;
        }

        cancellationToken.ThrowIfCancellationRequested();
        timeoutCts.Cancel();
        return null;
    }

    private static IEnumerable<AutomationElement> EnumerateFocusableChain(AutomationElement start)
    {
        var current = start;
        for (var depth = 0; depth < 5 && current is not null; depth++)
        {
            yield return current;
            current = TreeWalker.ControlViewWalker.GetParent(current);
        }
    }

    private static SelectionSnapshot? TryReadFromTextPattern(AutomationElement element)
    {
        if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var patternObject) ||
            patternObject is not TextPattern textPattern)
        {
            return null;
        }

        var fragments = new List<string>();
        double rightMostEdge = double.MinValue;
        int? anchorX = null;
        int? anchorY = null;

        foreach (var range in textPattern.GetSelection())
        {
            var text = range.GetText(-1)?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                fragments.Add(text);
            }

            UpdateAnchorFromBoundingRectangles(
                range.GetBoundingRectangles(),
                ref rightMostEdge,
                ref anchorX,
                ref anchorY);
        }

        var selectedText = string.Join(Environment.NewLine, fragments).Trim();
        return string.IsNullOrWhiteSpace(selectedText)
            ? null
            : new SelectionSnapshot(selectedText, anchorX, anchorY);
    }

    private static SelectionSnapshot? TryReadFromSelectionPattern(AutomationElement element)
    {
        if (!element.TryGetCurrentPattern(SelectionPattern.Pattern, out var patternObject) ||
            patternObject is not SelectionPattern selectionPattern)
        {
            return null;
        }

        var names = new List<string>();
        double rightMostEdge = double.MinValue;
        int? anchorX = null;
        int? anchorY = null;

        foreach (var selectedElement in selectionPattern.Current.GetSelection())
        {
            var name = selectedElement.Current.Name?.Trim();
            if (!string.IsNullOrWhiteSpace(name) &&
                !names.Contains(name, StringComparer.Ordinal))
            {
                names.Add(name);
            }

            var rect = selectedElement.Current.BoundingRectangle;
            UpdateAnchorCandidate(
                rect.Left,
                rect.Top,
                rect.Width,
                rect.Height,
                ref rightMostEdge,
                ref anchorX,
                ref anchorY);
        }

        var selectedText = string.Join(
            " ",
            names
        ).Trim();

        return string.IsNullOrWhiteSpace(selectedText)
            ? null
            : new SelectionSnapshot(selectedText, anchorX, anchorY);
    }

    private static void UpdateAnchorFromBoundingRectangles(
        System.Windows.Rect[] boundingRectangles,
        ref double rightMostEdge,
        ref int? anchorX,
        ref int? anchorY)
    {
        foreach (var rect in boundingRectangles)
        {
            UpdateAnchorCandidate(
                rect.Left,
                rect.Top,
                rect.Width,
                rect.Height,
                ref rightMostEdge,
                ref anchorX,
                ref anchorY);
        }
    }

    private static void UpdateAnchorCandidate(
        double left,
        double top,
        double width,
        double height,
        ref double rightMostEdge,
        ref int? anchorX,
        ref int? anchorY)
    {
        if (double.IsNaN(left) ||
            double.IsNaN(top) ||
            double.IsNaN(width) ||
            double.IsNaN(height) ||
            double.IsInfinity(left) ||
            double.IsInfinity(top) ||
            double.IsInfinity(width) ||
            double.IsInfinity(height) ||
            width <= 0 ||
            height <= 0)
        {
            return;
        }

        var right = left + width;
        if (right <= rightMostEdge)
        {
            return;
        }

        rightMostEdge = right;
        anchorX = (int)Math.Round(right);
        anchorY = (int)Math.Round(top + (height / 2));
    }
}
