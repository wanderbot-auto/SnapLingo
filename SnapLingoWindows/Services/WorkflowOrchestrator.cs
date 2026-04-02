namespace SnapLingoWindows.Services;

public sealed class WorkflowOrchestrator
{
    private readonly WorkflowStateStore store;
    private readonly ISelectionCaptureService captureService;
    private readonly IProviderClientFactory providerClientFactory;
    private readonly LocalizationService localizer;
    private readonly Action requestPanelPresentation;
    private CancellationTokenSource? activeCts;
    private string? currentInput;

    public WorkflowOrchestrator(
        WorkflowStateStore store,
        ISelectionCaptureService captureService,
        IProviderClientFactory providerClientFactory,
        LocalizationService localizer,
        Action requestPanelPresentation)
    {
        this.store = store;
        this.captureService = captureService;
        this.providerClientFactory = providerClientFactory;
        this.localizer = localizer;
        this.requestPanelPresentation = requestPanelPresentation;
    }

    public async Task HandleHotkeyAsync()
    {
        CancelActiveWork();
        activeCts = new CancellationTokenSource();
        var cancellationToken = activeCts.Token;

        store.ResetForNewSession();
        var capturedText = await captureService.TryCaptureSelectionTextAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(capturedText))
        {
            requestPanelPresentation();
            await ProcessAsync(capturedText, "source_auto", cancellationToken);
            return;
        }

        var captureOutcome = await captureService.CaptureSelectionAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (captureOutcome is not SelectionCaptureOutcome.RequiresClipboardFallback fallback)
        {
            return;
        }

        store.PresentClipboardFallback();
        requestPanelPresentation();
        var clipboardText = await captureService.WaitForClipboardChangeAsync(fallback.ChangeCount, cancellationToken);
        if (!string.IsNullOrWhiteSpace(clipboardText))
        {
            await ProcessAsync(clipboardText, "source_clipboard", cancellationToken);
            return;
        }

        store.ShowError(localizer.Get("error_no_copied_text"));
    }

    public async Task HandleCapturedSelectionAsync(string text, string sourceLabelKey = "source_auto")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        CancelActiveWork();
        activeCts = new CancellationTokenSource();
        requestPanelPresentation();
        await ProcessAsync(text.Trim(), sourceLabelKey, activeCts.Token);
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
        var provider = providerClientFactory.CurrentClient();

        try
        {
            switch (selectedMode)
            {
                case TranslationMode.Translate:
                    var translation = await provider.TranslateAsync(text, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    store.ShowPartialTranslation(translation.Text);
                    try
                    {
                        var polishedFromTranslation = await provider.PolishAsync(translation.Text, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        store.ShowFinalResult(polishedFromTranslation.Text);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        store.ShowPartialTranslationFallback(localizer.Get("state_partial_kept_status"));
                    }
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
