using Microsoft.UI.Dispatching;

namespace SnapLingoWindows.Services;

public sealed class AutoSelectionMonitorService : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(450);
    private static readonly TimeSpan DuplicateSuppressWindow = TimeSpan.FromSeconds(6);
    private const int DragThreshold = 6;

    private readonly DispatcherQueueTimer timer;
    private readonly ISelectionCaptureService captureService;
    private readonly Func<string, Task> onSelectionDetected;
    private bool isCheckingSelection;
    private bool wasLeftMouseDown;
    private NativeMethods.POINT dragStartPoint;
    private bool hasDragStartPoint;
    private bool dragSelectionLikely;
    private string? lastTriggeredText;
    private DateTimeOffset lastTriggeredAt = DateTimeOffset.MinValue;

    public AutoSelectionMonitorService(
        DispatcherQueue dispatcherQueue,
        ISelectionCaptureService captureService,
        Func<string, Task> onSelectionDetected)
    {
        this.captureService = captureService;
        this.onSelectionDetected = onSelectionDetected;
        timer = dispatcherQueue.CreateTimer();
        timer.Interval = PollInterval;
        timer.IsRepeating = true;
        timer.Tick += OnTick;
    }

    public void Start()
    {
        if (!timer.IsRunning)
        {
            timer.Start();
        }
    }

    public void Dispose()
    {
        if (timer.IsRunning)
        {
            timer.Stop();
        }

        timer.Tick -= OnTick;
    }

    private async void OnTick(DispatcherQueueTimer sender, object args)
    {
        var isLeftMouseDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & 0x8000) != 0;
        UpdatePointerTracking(isLeftMouseDown);

        if (isCheckingSelection || !(wasLeftMouseDown && !isLeftMouseDown && dragSelectionLikely))
        {
            wasLeftMouseDown = isLeftMouseDown;
            return;
        }

        isCheckingSelection = true;

        try
        {
            var text = await captureService.TryCaptureSelectionTextAsync(
                CancellationToken.None,
                allowSimulatedCopyFallback: true);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            text = text.Trim();
            if (ShouldSuppress(text))
            {
                return;
            }

            lastTriggeredText = text;
            lastTriggeredAt = DateTimeOffset.UtcNow;
            await onSelectionDetected(text);
        }
        catch
        {
            // Keep the monitor passive; capture failures should not crash the app.
        }
        finally
        {
            wasLeftMouseDown = isLeftMouseDown;
            isCheckingSelection = false;
        }
    }

    private void UpdatePointerTracking(bool isLeftMouseDown)
    {
        if (isLeftMouseDown && !wasLeftMouseDown)
        {
            if (NativeMethods.GetCursorPos(out var point))
            {
                dragStartPoint = point;
                hasDragStartPoint = true;
                dragSelectionLikely = false;
            }

            return;
        }

        if (isLeftMouseDown && hasDragStartPoint && NativeMethods.GetCursorPos(out var currentPoint))
        {
            var deltaX = Math.Abs(currentPoint.X - dragStartPoint.X);
            var deltaY = Math.Abs(currentPoint.Y - dragStartPoint.Y);
            if (deltaX >= DragThreshold || deltaY >= DragThreshold)
            {
                dragSelectionLikely = true;
            }

            return;
        }

        if (!isLeftMouseDown && wasLeftMouseDown)
        {
            hasDragStartPoint = false;
        }
    }

    private bool ShouldSuppress(string text)
    {
        return !string.IsNullOrWhiteSpace(lastTriggeredText) &&
               string.Equals(text, lastTriggeredText, StringComparison.Ordinal) &&
               DateTimeOffset.UtcNow - lastTriggeredAt < DuplicateSuppressWindow;
    }
}
