using SnapLingoWindows.Models;
using SnapLingoWindows.Services;
using SnapLingoWindows.Stores;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Localization catalog keeps English and Chinese keys aligned", LocalizationCatalogKeepsKeysAlignedAsync),
    ("Provider catalog uses shared curated defaults", ProviderCatalogUsesSharedDefaultsAsync),
    ("Workflow orchestrator keeps partial fallback when polish fails", WorkflowKeepsPartialFallbackAsync),
    ("Workflow orchestrator retry path replays capture flow", WorkflowRetryReplaysCaptureFlowAsync),
    ("Workflow orchestrator cancels in-flight work before new input", WorkflowCancelsInflightWorkAsync),
    ("Selection activation gate suppresses empty and duplicate text", SelectionActivationGateSuppressesEmptyAndDuplicateAsync),
};

var failures = new List<string>();
foreach (var (name, run) in tests)
{
    try
    {
        await run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception error)
    {
        failures.Add($"{name}: {error.Message}");
        Console.WriteLine($"FAIL {name}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine("Failures:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($" - {failure}");
    }

    return 1;
}

Console.WriteLine($"Passed {tests.Length} tests.");
return 0;

static Task LocalizationCatalogKeepsKeysAlignedAsync()
{
    var catalog = LocalizationCatalog.Shared;
    AssertEx.SetEqual(catalog.English.Keys, catalog.Chinese.Keys, "Localization keys diverged between English and Chinese resources.");
    AssertEx.True(catalog.English.Count > 0, "English localization table should not be empty.");
    AssertEx.True(catalog.Chinese.Count > 0, "Chinese localization table should not be empty.");
    return Task.CompletedTask;
}

static Task ProviderCatalogUsesSharedDefaultsAsync()
{
    var catalog = ProviderCatalog.Shared;

    AssertEx.Equal("gpt-4.1-mini", catalog.GetPreset(ProviderKind.OpenAI).Model);
    AssertEx.Equal(ProviderProtocolStyle.AnthropicMessages, catalog.GetPreset(ProviderKind.Anthropic).Style);
    AssertEx.Equal("glm-4.5-air", catalog.GetPreset(ProviderKind.ZhipuGLM).Model);
    AssertEx.Equal("kimi-k2-turbo-preview", catalog.GetPreset(ProviderKind.Kimi).Model);
    AssertEx.Equal("qwen3.5-flash", catalog.GetPreset(ProviderKind.AlibabaBailian).Model);
    AssertEx.Equal("doubao-seed-1-6-250615", catalog.GetPreset(ProviderKind.VolcengineArk).Model);
    AssertEx.SequenceEqual(
        new[] { "MiniMax-M2.5-highspeed", "MiniMax-M2.7" },
        catalog.GetPresetModels(ProviderKind.MiniMax),
        "MiniMax preset models should come from the shared manifest.");

    return Task.CompletedTask;
}

static async Task WorkflowKeepsPartialFallbackAsync()
{
    var localizer = new LocalizationService(AppLanguage.English, BuildTestCatalog());
    var store = new WorkflowStateStore(localizer);
    var captureService = new FakeSelectionCaptureService();
    var client = new FakeProviderClient
    {
        TranslateAsyncImpl = (text, _) => Task.FromResult(new ProviderOutput("Hello world")),
        PolishAsyncImpl = (_, _) => throw new ProviderException("final polish failed"),
    };
    var orchestrator = new WorkflowOrchestrator(
        store,
        captureService,
        new FakeProviderClientFactory(client),
        localizer,
        () => { });

    await orchestrator.HandleCapturedSelectionAsync("你好");

    AssertEx.Equal(WorkflowPhase.Partial, store.Phase);
    AssertEx.Equal("Hello world", store.PrimaryText);
    AssertEx.Equal(localizer.Get("state_partial_kept_status"), store.SecondaryStatus);
    AssertEx.True(store.CanCopy, "Partial fallback should still allow copying the quick translation.");
}

static async Task WorkflowRetryReplaysCaptureFlowAsync()
{
    var localizer = new LocalizationService(AppLanguage.English, BuildTestCatalog());
    var store = new WorkflowStateStore(localizer);
    var captureService = new FakeSelectionCaptureService
    {
        TryCaptureSelectionTextAsyncImpl = _ => Task.FromResult<string?>(null),
        CaptureSelectionAsyncImpl = _ => Task.FromResult<SelectionCaptureOutcome>(new SelectionCaptureOutcome.RequiresClipboardFallback(42)),
        WaitForClipboardChangeAsyncImpl = (_, _) => Task.FromResult<string?>("copied text"),
    };
    var client = new FakeProviderClient
    {
        PolishAsyncImpl = (text, _) => Task.FromResult(new ProviderOutput("copied text polished")),
    };
    var orchestrator = new WorkflowOrchestrator(
        store,
        captureService,
        new FakeProviderClientFactory(client),
        localizer,
        () => { });

    await orchestrator.RetryAsync();

    AssertEx.Equal(1, captureService.CaptureSelectionCallCount);
    AssertEx.Equal(1, captureService.WaitForClipboardCallCount);
    AssertEx.Equal(WorkflowPhase.Ready, store.Phase);
    AssertEx.Equal("copied text polished", store.PrimaryText);
}

static async Task WorkflowCancelsInflightWorkAsync()
{
    var localizer = new LocalizationService(AppLanguage.English, BuildTestCatalog());
    var store = new WorkflowStateStore(localizer);
    var captureService = new FakeSelectionCaptureService();
    var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var firstCanceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    var client = new FakeProviderClient
    {
        TranslateAsyncImpl = async (text, cancellationToken) =>
        {
            if (text == "第一条")
            {
                firstStarted.TrySetResult(true);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    firstCanceled.TrySetResult(true);
                    throw;
                }
            }

            return new ProviderOutput($"{text}-translated");
        },
        PolishAsyncImpl = (text, _) => Task.FromResult(new ProviderOutput($"{text}-polished")),
    };
    var orchestrator = new WorkflowOrchestrator(
        store,
        captureService,
        new FakeProviderClientFactory(client),
        localizer,
        () => { });

    var firstRun = orchestrator.HandleCapturedSelectionAsync("第一条");
    await firstStarted.Task;
    await orchestrator.HandleCapturedSelectionAsync("第二条");
    await firstRun;

    AssertEx.True(await firstCanceled.Task.WaitAsync(TimeSpan.FromSeconds(1)), "The first provider request should have been cancelled.");
    AssertEx.Equal(WorkflowPhase.Ready, store.Phase);
    AssertEx.Equal("第二条-translated-polished", store.PrimaryText);
}

static Task SelectionActivationGateSuppressesEmptyAndDuplicateAsync()
{
    var gate = new SelectionActivationGate(TimeSpan.FromSeconds(5));
    var now = DateTimeOffset.Parse("2026-04-01T12:00:00Z");

    AssertEx.Null(gate.TryCreateRequest(new SelectionSnapshot("   "), 10, 20, now), "Empty text should never trigger auto-selection activation.");

    var first = gate.TryCreateRequest(new SelectionSnapshot("hello"), 10, 20, now);
    AssertEx.NotNull(first, "Non-empty text should produce an activation request.");
    AssertEx.Equal(10, first!.AnchorX);
    AssertEx.Equal(20, first.AnchorY);

    var duplicate = gate.TryCreateRequest(new SelectionSnapshot("hello"), 30, 40, now.AddSeconds(1));
    AssertEx.Null(duplicate, "Duplicate text inside the suppress window should be ignored.");

    var later = gate.TryCreateRequest(new SelectionSnapshot("hello", 55, 66), 30, 40, now.AddSeconds(6));
    AssertEx.NotNull(later, "The suppress window should expire.");
    AssertEx.Equal(55, later!.AnchorX);
    AssertEx.Equal(66, later.AnchorY);

    return Task.CompletedTask;
}

static LocalizationCatalog BuildTestCatalog()
{
    var english = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["source_auto"] = "Auto",
        ["source_clipboard"] = "Clipboard",
        ["state_ready_title"] = "Ready",
        ["state_ready_status"] = "Ready status",
        ["state_capturing_title"] = "Capturing",
        ["state_capturing_status"] = "Capturing status",
        ["state_clipboard_title"] = "Clipboard",
        ["state_clipboard_primary"] = "Clipboard primary",
        ["state_clipboard_status"] = "Clipboard status",
        ["state_translate_title"] = "Quick Translation",
        ["state_translate_status"] = "Translating",
        ["state_polish_title"] = "Polished Version",
        ["state_polish_status"] = "Polishing",
        ["state_partial_status"] = "Optimizing",
        ["state_partial_kept_status"] = "Kept the quick translation.",
        ["state_error_title"] = "Error",
        ["state_error_status"] = "Error status",
        ["state_copied_status"] = "Copied",
        ["error_no_copied_text"] = "No copied text",
        ["error_empty_clipboard"] = "Clipboard empty",
    };

    return new LocalizationCatalog(english, new Dictionary<string, string>(english, StringComparer.Ordinal));
}

static class AssertEx
{
    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(message ?? $"Expected: {expected}. Actual: {actual}.");
        }
    }

    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Null(object? value, string message)
    {
        if (value is not null)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void NotNull(object? value, string message)
    {
        if (value is null)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void SetEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
    {
        var left = new HashSet<T>(expected);
        var right = new HashSet<T>(actual);
        if (!left.SetEquals(right))
        {
            throw new InvalidOperationException(message);
        }
    }
}

sealed class FakeProviderClientFactory(IProviderClient client) : IProviderClientFactory
{
    public IProviderClient CurrentClient() => client;
}

sealed class FakeProviderClient : IProviderClient
{
    public Func<string, CancellationToken, Task<ProviderOutput>> TranslateAsyncImpl { get; set; }
        = (text, _) => Task.FromResult(new ProviderOutput(text));

    public Func<string, CancellationToken, Task<ProviderOutput>> PolishAsyncImpl { get; set; }
        = (text, _) => Task.FromResult(new ProviderOutput(text));

    public Task<ProviderOutput> TranslateAsync(string text, CancellationToken cancellationToken)
        => TranslateAsyncImpl(text, cancellationToken);

    public Task<ProviderOutput> PolishAsync(string text, CancellationToken cancellationToken)
        => PolishAsyncImpl(text, cancellationToken);
}

sealed class FakeSelectionCaptureService : ISelectionCaptureService
{
    public Func<CancellationToken, Task<SelectionCaptureOutcome>> CaptureSelectionAsyncImpl { get; set; }
        = _ => Task.FromResult<SelectionCaptureOutcome>(new SelectionCaptureOutcome.RequiresClipboardFallback(0));

    public Func<CancellationToken, Task<SelectionSnapshot?>> TryCaptureSelectionSnapshotAsyncImpl { get; set; }
        = _ => Task.FromResult<SelectionSnapshot?>(null);

    public Func<CancellationToken, Task<string?>> TryCaptureSelectionTextAsyncImpl { get; set; }
        = _ => Task.FromResult<string?>(null);

    public Func<uint, CancellationToken, Task<string?>> WaitForClipboardChangeAsyncImpl { get; set; }
        = (_, _) => Task.FromResult<string?>(null);

    public Func<Task<string?>> ReadClipboardTextAsyncImpl { get; set; }
        = () => Task.FromResult<string?>(null);

    public int CaptureSelectionCallCount { get; private set; }
    public int WaitForClipboardCallCount { get; private set; }

    public Task<SelectionCaptureOutcome> CaptureSelectionAsync(CancellationToken cancellationToken)
    {
        CaptureSelectionCallCount++;
        return CaptureSelectionAsyncImpl(cancellationToken);
    }

    public Task<SelectionSnapshot?> TryCaptureSelectionSnapshotAsync(CancellationToken cancellationToken)
        => TryCaptureSelectionSnapshotAsyncImpl(cancellationToken);

    public Task<string?> TryCaptureSelectionTextAsync(CancellationToken cancellationToken)
        => TryCaptureSelectionTextAsyncImpl(cancellationToken);

    public Task<string?> WaitForClipboardChangeAsync(uint afterChangeCount, CancellationToken cancellationToken)
    {
        WaitForClipboardCallCount++;
        return WaitForClipboardChangeAsyncImpl(afterChangeCount, cancellationToken);
    }

    public Task<string?> ReadClipboardTextAsync()
        => ReadClipboardTextAsyncImpl();
}
