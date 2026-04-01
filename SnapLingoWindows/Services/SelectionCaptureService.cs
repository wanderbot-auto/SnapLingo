using System.Windows.Automation;
using Windows.ApplicationModel.DataTransfer;

namespace SnapLingoWindows.Services;

public interface ISelectionCaptureService
{
    Task<SelectionCaptureOutcome> CaptureSelectionAsync(CancellationToken cancellationToken);
    Task<string?> TryCaptureSelectionTextAsync(CancellationToken cancellationToken, bool allowSimulatedCopyFallback);
    Task<string?> WaitForClipboardChangeAsync(uint afterChangeCount, CancellationToken cancellationToken);
    Task<string?> ReadClipboardTextAsync();
}

public sealed class ClipboardSelectionCaptureService : ISelectionCaptureService
{
    public async Task<SelectionCaptureOutcome> CaptureSelectionAsync(CancellationToken cancellationToken)
    {
        var directSelection = await TryCaptureDirectSelectionAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(directSelection))
        {
            return new SelectionCaptureOutcome.Text(directSelection);
        }

        return new SelectionCaptureOutcome.RequiresClipboardFallback(NativeMethods.GetClipboardSequenceNumber());
    }

    public async Task<string?> TryCaptureSelectionTextAsync(CancellationToken cancellationToken, bool allowSimulatedCopyFallback)
    {
        var directSelection = await TryCaptureDirectSelectionAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(directSelection))
        {
            return directSelection.Trim();
        }

        if (!allowSimulatedCopyFallback || !CanTargetForegroundSelection())
        {
            return null;
        }

        var changeCount = NativeMethods.GetClipboardSequenceNumber();
        SimulateCopyShortcut();
        return await WaitForClipboardChangeAsync(changeCount, cancellationToken);
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

    private static Task<string?> TryCaptureDirectSelectionAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var focusedElement = AutomationElement.FocusedElement;
                if (focusedElement is null)
                {
                    return (string?)null;
                }

                if (focusedElement.Current.ProcessId == Environment.ProcessId)
                {
                    return null;
                }

                foreach (var element in EnumerateFocusableChain(focusedElement))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var text = TryReadFromTextPattern(element);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }

                    text = TryReadFromSelectionPattern(element);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
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

            return null;
        }, cancellationToken);
    }

    private static bool CanTargetForegroundSelection()
    {
        var foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow == 0)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(foregroundWindow, out var processId);
        return processId != 0 && processId != Environment.ProcessId;
    }

    private static void SimulateCopyShortcut()
    {
        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, 0, 0);
        NativeMethods.keybd_event(NativeMethods.VK_C, 0, 0, 0);
        NativeMethods.keybd_event(NativeMethods.VK_C, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0, NativeMethods.KEYEVENTF_KEYUP, 0);
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

    private static string? TryReadFromTextPattern(AutomationElement element)
    {
        if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var patternObject) ||
            patternObject is not TextPattern textPattern)
        {
            return null;
        }

        var selectedText = string.Join(
            Environment.NewLine,
            textPattern.GetSelection()
                .Select(range => range.GetText(-1)?.Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text))
        ).Trim();

        return string.IsNullOrWhiteSpace(selectedText) ? null : selectedText;
    }

    private static string? TryReadFromSelectionPattern(AutomationElement element)
    {
        if (!element.TryGetCurrentPattern(SelectionPattern.Pattern, out var patternObject) ||
            patternObject is not SelectionPattern selectionPattern)
        {
            return null;
        }

        var selectedText = string.Join(
            " ",
            selectionPattern.Current.GetSelection()
                .Select(selectedElement => selectedElement.Current.Name?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
        ).Trim();

        return string.IsNullOrWhiteSpace(selectedText) ? null : selectedText;
    }
}
