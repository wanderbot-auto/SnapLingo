using Microsoft.UI.Dispatching;
using SnapLingoWindows.Models;

namespace SnapLingoWindows.Services;

public sealed class AutoSelectionMonitorService : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan PassiveSelectionCheckInterval = TimeSpan.FromMilliseconds(360);
    private static readonly TimeSpan PostReleaseRetryDelay = TimeSpan.FromMilliseconds(90);
    private const int DragThreshold = 6;
    private const int DoubleClickDistanceThreshold = 18;
    private const int PostReleaseRetryCount = 4;

    private readonly DispatcherQueueTimer timer;
    private readonly ISelectionCaptureService captureService;
    private readonly Func<SelectionActivationRequest, Task> onSelectionDetected;
    private readonly Func<SelectionActivationSettings> activationSettingsAccessor;
    private SelectionActivationGate activationGate;
    private SelectionActivationSettings configuredActivationSettings;
    private bool isCheckingSelection;
    private bool wasLeftMouseDown;
    private NativeMethods.POINT dragStartPoint;
    private bool hasDragStartPoint;
    private bool dragSelectionLikely;
    private bool doubleClickSelectionLikely;
    private string? lastObservedSelectionText;
    private bool hasObservedSelectionState;
    private DateTimeOffset lastPassiveSelectionCheckAt = DateTimeOffset.MinValue;
    private NativeMethods.POINT lastClickReleasePoint;
    private DateTimeOffset lastClickReleaseAt = DateTimeOffset.MinValue;
    private bool hasLastClickReleasePoint;

    public AutoSelectionMonitorService(
        DispatcherQueue dispatcherQueue,
        ISelectionCaptureService captureService,
        Func<SelectionActivationRequest, Task> onSelectionDetected,
        Func<SelectionActivationSettings>? activationSettingsAccessor = null)
    {
        this.captureService = captureService;
        this.onSelectionDetected = onSelectionDetected;
        this.activationSettingsAccessor = activationSettingsAccessor ?? (() => SelectionActivationSettings.Default);
        configuredActivationSettings = this.activationSettingsAccessor();
        activationGate = new SelectionActivationGate(
            debounceWindow: configuredActivationSettings.DebounceWindow,
            minimumWordCount: configuredActivationSettings.MinimumWordCount);
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
        var now = DateTimeOffset.UtcNow;
        var isLeftMouseDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LBUTTON) & 0x8000) != 0;
        UpdatePointerTracking(isLeftMouseDown, now);
        var pointerSelectionReleased = wasLeftMouseDown && !isLeftMouseDown;
        var shouldRunPassiveCheck = !isLeftMouseDown && now - lastPassiveSelectionCheckAt >= PassiveSelectionCheckInterval;

        if (isCheckingSelection || !(pointerSelectionReleased || shouldRunPassiveCheck))
        {
            wasLeftMouseDown = isLeftMouseDown;
            return;
        }

        isCheckingSelection = true;

        try
        {
            if (shouldRunPassiveCheck)
            {
                lastPassiveSelectionCheckAt = now;
            }

            var selection = pointerSelectionReleased
                ? await TryCaptureSelectionAfterReleaseAsync()
                : await captureService.TryCaptureSelectionSnapshotAsync(CancellationToken.None);

            var request = BuildActivationRequest(selection, pointerSelectionReleased);
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

    private SelectionActivationRequest? BuildActivationRequest(SelectionSnapshot? selection, bool pointerSelectionReleased)
    {
        RefreshActivationGateConfiguration();
        var text = selection?.Text?.Trim();
        var selectionChanged = !string.Equals(text, lastObservedSelectionText, StringComparison.Ordinal);
        var shouldContinuePendingDebounce = activationGate.HasPendingCandidate(text);
        var canTriggerPassiveSelection = hasObservedSelectionState && selectionChanged;

        lastObservedSelectionText = string.IsNullOrWhiteSpace(text) ? null : text;
        hasObservedSelectionState = true;

        if (!(pointerSelectionReleased || canTriggerPassiveSelection || shouldContinuePendingDebounce))
        {
            return null;
        }

        var selectionWithTrimmedText = string.IsNullOrWhiteSpace(text)
            ? null
            : new SelectionSnapshot(text, selection?.AnchorX, selection?.AnchorY);
        var anchor = ResolveAnchorPoint(selectionWithTrimmedText);
        return activationGate.TryCreateRequest(selectionWithTrimmedText, anchor.X, anchor.Y, DateTimeOffset.UtcNow);
    }

    private void RefreshActivationGateConfiguration()
    {
        var currentSettings = activationSettingsAccessor();
        if (currentSettings == configuredActivationSettings)
        {
            return;
        }

        configuredActivationSettings = currentSettings;
        activationGate = new SelectionActivationGate(
            debounceWindow: currentSettings.DebounceWindow,
            minimumWordCount: currentSettings.MinimumWordCount);
    }

    private async Task<SelectionSnapshot?> TryCaptureSelectionAfterReleaseAsync()
    {
        for (var attempt = 0; attempt < PostReleaseRetryCount; attempt++)
        {
            var selection = await captureService.TryCaptureSelectionSnapshotAsync(CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(selection?.Text))
            {
                return selection;
            }

            if (attempt + 1 < PostReleaseRetryCount)
            {
                await Task.Delay(PostReleaseRetryDelay);
            }
        }

        return null;
    }

    private void UpdatePointerTracking(bool isLeftMouseDown, DateTimeOffset now)
    {
        if (isLeftMouseDown && !wasLeftMouseDown)
        {
            if (NativeMethods.GetCursorPos(out var point))
            {
                dragStartPoint = point;
                hasDragStartPoint = true;
                dragSelectionLikely = false;
            }

            doubleClickSelectionLikely = false;

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
            if (!dragSelectionLikely)
            {
                var releasePoint = ResolveReleasePoint();
                doubleClickSelectionLikely = IsDoubleClickRelease(releasePoint, now);
                lastClickReleasePoint = releasePoint;
                lastClickReleaseAt = now;
                hasLastClickReleasePoint = true;
            }
            else
            {
                doubleClickSelectionLikely = false;
            }

            hasDragStartPoint = false;
        }
    }

    private NativeMethods.POINT ResolveReleasePoint()
    {
        if (NativeMethods.GetCursorPos(out var point))
        {
            return point;
        }

        return dragStartPoint;
    }

    private bool IsDoubleClickRelease(NativeMethods.POINT releasePoint, DateTimeOffset now)
    {
        if (!hasLastClickReleasePoint)
        {
            return false;
        }

        var doubleClickWindow = TimeSpan.FromMilliseconds(NativeMethods.GetDoubleClickTime());
        if (now - lastClickReleaseAt > doubleClickWindow)
        {
            return false;
        }

        var deltaX = Math.Abs(releasePoint.X - lastClickReleasePoint.X);
        var deltaY = Math.Abs(releasePoint.Y - lastClickReleasePoint.Y);
        return deltaX <= DoubleClickDistanceThreshold && deltaY <= DoubleClickDistanceThreshold;
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
