namespace SnapLingoWindows.Services;

public sealed class WorkflowOrchestrator
{
    private readonly WorkflowStateStore store;
    private readonly ISelectionCaptureService captureService;
    private readonly ProviderRegistry providerRegistry;
    private readonly LocalizationService localizer;
    private CancellationTokenSource? activeCts;
    private string? currentInput;

    public WorkflowOrchestrator(
        WorkflowStateStore store,
        ISelectionCaptureService captureService,
        ProviderRegistry providerRegistry,
        LocalizationService localizer)
    {
        this.store = store;
        this.captureService = captureService;
        this.providerRegistry = providerRegistry;
        this.localizer = localizer;
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
                await ProcessAsync(text.Value, "source_auto", cancellationToken);
                break;

            case SelectionCaptureOutcome.RequiresClipboardFallback fallback:
                store.PresentClipboardFallback();
                var clipboardText = await captureService.WaitForClipboardChangeAsync(fallback.ChangeCount, cancellationToken);
                if (!string.IsNullOrWhiteSpace(clipboardText))
                {
                    await ProcessAsync(clipboardText, "source_clipboard", cancellationToken);
                    return;
                }

                store.ShowError(localizer.Get("error_no_copied_text"));
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
            store.ShowError(localizer.Get("error_empty_clipboard"));
            return;
        }

        CancelActiveWork();
        activeCts = new CancellationTokenSource();
        await ProcessAsync(text, "source_clipboard", activeCts.Token);
    }

    private async Task ProcessAsync(string text, string sourceLabelKey, CancellationToken cancellationToken)
    {
        currentInput = text;
        var mode = ModeDetector.Detect(text);
        store.BeginProcessing(text, mode, sourceLabelKey);
        await RunProviderPipelineAsync(text, mode, sourceLabelKey, cancellationToken);
    }

    private async Task RunProviderPipelineAsync(string text, TranslationMode selectedMode, string sourceLabelKey, CancellationToken cancellationToken)
    {
        store.BeginProcessing(text, selectedMode, sourceLabelKey);
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
                    store.ShowFinalResult(polishedFromTranslation.Text);
                    break;

                case TranslationMode.Polish:
                    var polished = await provider.PolishAsync(text, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    store.ShowFinalResult(polished.Text);
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
