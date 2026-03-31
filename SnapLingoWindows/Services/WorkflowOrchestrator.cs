namespace SnapLingoWindows.Services;

public sealed class WorkflowOrchestrator
{
    private readonly WorkflowStateStore store;
    private readonly ISelectionCaptureService captureService;
    private readonly ProviderRegistry providerRegistry;
    private CancellationTokenSource? activeCts;
    private string? currentInput;

    public WorkflowOrchestrator(
        WorkflowStateStore store,
        ISelectionCaptureService captureService,
        ProviderRegistry providerRegistry)
    {
        this.store = store;
        this.captureService = captureService;
        this.providerRegistry = providerRegistry;
    }

    public async Task HandleHotkeyAsync()
    {
        CancelActiveWork();
        activeCts = new CancellationTokenSource();
        var cancellationToken = activeCts.Token;

        store.ResetForNewSession();
        var captureOutcome = await captureService.CaptureSelectionAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        switch (captureOutcome)
        {
            case SelectionCaptureOutcome.Text text:
                await ProcessAsync(text.Value, "Auto", cancellationToken);
                break;

            case SelectionCaptureOutcome.RequiresClipboardFallback fallback:
                store.PresentClipboardFallback();
                var clipboardText = await captureService.WaitForClipboardChangeAsync(fallback.ChangeCount, cancellationToken);
                if (!string.IsNullOrWhiteSpace(clipboardText))
                {
                    await ProcessAsync(clipboardText, "Clipboard", cancellationToken);
                    return;
                }

                store.ShowError("SnapLingo never received copied text. Press the hotkey again or use the current clipboard.");
                break;
        }
    }

    public async Task RetryAsync()
    {
        await HandleHotkeyAsync();
    }

    public async Task SwitchModeAsync(TranslationMode mode)
    {
        if (string.IsNullOrWhiteSpace(currentInput))
        {
            return;
        }

        CancelActiveWork();
        activeCts = new CancellationTokenSource();
        await RunProviderPipelineAsync(currentInput, mode, "Manual", activeCts.Token);
    }

    public async Task UseCurrentClipboardAsync()
    {
        var text = await captureService.ReadClipboardTextAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            store.ShowError("The clipboard does not contain text yet. Copy some text first and try again.");
            return;
        }

        CancelActiveWork();
        activeCts = new CancellationTokenSource();
        await ProcessAsync(text, "Clipboard", activeCts.Token);
    }

    private async Task ProcessAsync(string text, string sourceLabel, CancellationToken cancellationToken)
    {
        currentInput = text;
        var mode = ModeDetector.Detect(text);
        store.BeginProcessing(text, mode, sourceLabel);
        await RunProviderPipelineAsync(text, mode, sourceLabel, cancellationToken);
    }

    private async Task RunProviderPipelineAsync(string text, TranslationMode selectedMode, string sourceLabel, CancellationToken cancellationToken)
    {
        store.BeginProcessing(text, selectedMode, sourceLabel);
        var provider = providerRegistry.CurrentClient();

        try
        {
            switch (selectedMode)
            {
                case TranslationMode.Translate:
                    var translation = await provider.TranslateAsync(text, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    store.ShowPartialTranslation(translation.Text);

                    var polishedFromTranslation = await provider.PolishAsync(translation.Text, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    store.ShowFinalResult("Polished Version", polishedFromTranslation.Text);
                    break;

                case TranslationMode.Polish:
                    var polished = await provider.PolishAsync(text, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    store.ShowFinalResult("Polished Version", polished.Text);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ProviderException error)
        {
            store.ShowError(error.Message);
        }
        catch (Exception error)
        {
            store.ShowError(error.Message);
        }
    }

    private void CancelActiveWork()
    {
        activeCts?.Cancel();
        activeCts?.Dispose();
        activeCts = null;
    }
}
