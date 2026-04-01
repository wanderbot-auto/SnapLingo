using Microsoft.UI.Dispatching;

namespace SnapLingoWindows.Services;

public sealed class AutoSelectionMonitorService : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(450);
    private static readonly TimeSpan DuplicateSuppressWindow = TimeSpan.FromSeconds(6);

    private readonly DispatcherQueueTimer timer;
    private readonly ISelectionCaptureService captureService;
    private readonly Func<string, Task> onSelectionDetected;
    private bool isCheckingSelection;
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
        if (isCheckingSelection)
        {
            return;
        }

        isCheckingSelection = true;

        try
        {
            var outcome = await captureService.CaptureSelectionAsync(CancellationToken.None);
            if (outcome is not SelectionCaptureOutcome.Text textOutcome)
            {
                return;
            }

            var text = textOutcome.Value.Trim();
            if (string.IsNullOrWhiteSpace(text) || ShouldSuppress(text))
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
            isCheckingSelection = false;
        }
    }

    private bool ShouldSuppress(string text)
    {
        return !string.IsNullOrWhiteSpace(lastTriggeredText) &&
               string.Equals(text, lastTriggeredText, StringComparison.Ordinal) &&
               DateTimeOffset.UtcNow - lastTriggeredAt < DuplicateSuppressWindow;
    }
}
