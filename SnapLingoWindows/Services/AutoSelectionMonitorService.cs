using Microsoft.UI.Dispatching;
using SnapLingoWindows.Models;

namespace SnapLingoWindows.Services;

public sealed class AutoSelectionMonitorService : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(120);
    private const int DragThreshold = 6;

    private readonly DispatcherQueueTimer timer;
    private readonly ISelectionCaptureService captureService;
    private readonly Func<SelectionActivationRequest, Task> onSelectionDetected;
    private readonly SelectionActivationGate activationGate;
    private bool isCheckingSelection;
    private bool wasLeftMouseDown;
    private NativeMethods.POINT dragStartPoint;
    private bool hasDragStartPoint;
    private bool dragSelectionLikely;

    public AutoSelectionMonitorService(
        DispatcherQueue dispatcherQueue,
        ISelectionCaptureService captureService,
        Func<SelectionActivationRequest, Task> onSelectionDetected)
    {
        this.captureService = captureService;
        this.onSelectionDetected = onSelectionDetected;
        activationGate = new SelectionActivationGate();
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
            var selection = await captureService.TryCaptureSelectionSnapshotAsync(CancellationToken.None);
            var anchorPoint = ResolveAnchorPoint(selection);
            var request = activationGate.TryCreateRequest(selection, anchorPoint.X, anchorPoint.Y, DateTimeOffset.UtcNow);
            if (request is null)
            {
                return;
            }

            _ = NotifySelectionDetectedAsync(request);
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

    private NativeMethods.POINT ResolveAnchorPoint(SelectionSnapshot? selection)
    {
        if (selection?.AnchorX is int anchorX && selection.AnchorY is int anchorY)
        {
            return new NativeMethods.POINT
            {
                X = anchorX,
                Y = anchorY,
            };
        }

        if (NativeMethods.GetCursorPos(out var point))
        {
            return point;
        }

        return dragStartPoint;
    }

    private async Task NotifySelectionDetectedAsync(SelectionActivationRequest request)
    {
        try
        {
            await onSelectionDetected(request);
        }
        catch
        {
            // Provider or presentation failures should not break background selection monitoring.
        }
    }
}
