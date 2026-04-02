using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using SnapLingoWindows.Models;

namespace SnapLingoWindows;

public partial class App : Application
{
    private static readonly TimeSpan LauncherSelectionRetryDelay = TimeSpan.FromMilliseconds(120);
    private const int LauncherSelectionRetryCount = 16;

    private AutoSelectionMonitorService? autoSelectionMonitor;
    private ClipboardSelectionCaptureService selectionCaptureService = null!;
    private SelectionActivationRequest? pendingSelection;

    public MainViewModel ViewModel { get; private set; } = null!;
    public MainWindow SettingsWindow { get; private set; } = null!;
    public TranslationPanelWindow? TranslationWindow { get; private set; }
    public SelectionLauncherWindow? SelectionLauncher { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ViewModel = new MainViewModel();
        ViewModel.ShowRequested += OnShowRequested;
        SettingsWindow = new MainWindow(ViewModel);
        SettingsWindow.Activate();
        selectionCaptureService = new ClipboardSelectionCaptureService();
        autoSelectionMonitor = new AutoSelectionMonitorService(
            DispatcherQueue.GetForCurrentThread()!,
            selectionCaptureService,
            OnAutoSelectionDetectedAsync,
            () => ViewModel.AutoSelectionSettings.CurrentBehaviorSettings);
        autoSelectionMonitor.Start();
        DispatcherQueue.GetForCurrentThread()?.TryEnqueue(PreloadTranslationWindow);
        DispatcherQueue.GetForCurrentThread()?.TryEnqueue(PreloadSelectionLauncher);
    }

    public void ShowTranslationPanel()
    {
        pendingSelection = null;
        HideSelectionLauncher();
        EnsureTranslationWindow();

        try
        {
            TranslationWindow!.PresentPanel();
        }
        catch (COMException)
        {
            TranslationWindow = CreateTranslationWindow();
            TranslationWindow.PresentPanel();
        }
    }

    public void HideSettingsWindow()
    {
        SettingsWindow.HideWindow();
    }

    public void RestoreSettingsWindow()
    {
        SettingsWindow.ShowWindow();
    }

    public void HideTranslationPanel()
    {
        TranslationWindow?.HidePanel();
    }

    public void HideSelectionLauncher()
    {
        SelectionLauncher?.HideLauncher();
    }

    public void ExitApplication()
    {
        autoSelectionMonitor?.Dispose();
        autoSelectionMonitor = null;
        pendingSelection = null;
        ViewModel.ShowRequested -= OnShowRequested;
        Exit();
    }

    private Task OnAutoSelectionDetectedAsync(SelectionActivationRequest request)
    {
        pendingSelection = request;
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            _ = HydratePendingSelectionAsync();
        }

        HideTranslationPanel();
        ShowSelectionLauncher(request);
        return Task.CompletedTask;
    }

    private void OnShowRequested(object? sender, EventArgs e)
    {
        ShowTranslationPanel();
    }

    private void PreloadTranslationWindow()
    {
        EnsureTranslationWindow();
        TranslationWindow?.HidePanel();
    }

    private void PreloadSelectionLauncher()
    {
        EnsureSelectionLauncher();
        SelectionLauncher?.HideLauncher();
    }

    private void EnsureTranslationWindow()
    {
        if (TranslationWindow is null || TranslationWindow.IsClosed)
        {
            TranslationWindow = CreateTranslationWindow();
        }
    }

    private void EnsureSelectionLauncher()
    {
        if (SelectionLauncher is null || SelectionLauncher.IsClosed)
        {
            SelectionLauncher = new SelectionLauncherWindow(ViewModel.Localizer, OnSelectionLauncherInvoked, OnSelectionLauncherDismissed);
        }
    }

    private void ShowSelectionLauncher(SelectionActivationRequest request)
    {
        EnsureSelectionLauncher();

        try
        {
            SelectionLauncher!.Present(request);
        }
        catch (COMException)
        {
            SelectionLauncher = new SelectionLauncherWindow(ViewModel.Localizer, OnSelectionLauncherInvoked, OnSelectionLauncherDismissed);
            SelectionLauncher.Present(request);
        }
    }

    private TranslationPanelWindow CreateTranslationWindow()
    {
        var window = new TranslationPanelWindow(ViewModel);
        window.Closed += OnTranslationWindowClosed;
        return window;
    }

    private void OnTranslationWindowClosed(object sender, WindowEventArgs args)
    {
        if (sender is not TranslationPanelWindow window)
        {
            return;
        }

        window.Closed -= OnTranslationWindowClosed;

        if (ReferenceEquals(TranslationWindow, window))
        {
            TranslationWindow = null;
        }
    }

    private async void OnSelectionLauncherInvoked(TranslationMode mode)
    {
        if (pendingSelection is null)
        {
            HideSelectionLauncher();
            return;
        }

        var request = await ResolvePendingSelectionAsync();
        pendingSelection = null;
        HideSelectionLauncher();
        await ViewModel.HandleSelectionLauncherAsync(request.Text, mode);
    }

    private void OnSelectionLauncherDismissed()
    {
        pendingSelection = null;
        HideSelectionLauncher();
    }

    private async Task HydratePendingSelectionAsync()
    {
        for (var attempt = 0; attempt < LauncherSelectionRetryCount; attempt++)
        {
            var currentRequest = pendingSelection;
            if (currentRequest is null || !string.IsNullOrWhiteSpace(currentRequest.Text))
            {
                return;
            }

            var selection = await selectionCaptureService.TryCaptureSelectionSnapshotAsync(CancellationToken.None);
            var text = selection?.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (pendingSelection is SelectionActivationRequest pendingRequest && string.IsNullOrWhiteSpace(pendingRequest.Text))
                {
                    pendingSelection = pendingRequest with { Text = text };
                }

                return;
            }

            if (attempt + 1 < LauncherSelectionRetryCount)
            {
                await Task.Delay(LauncherSelectionRetryDelay);
            }
        }
    }

    private async Task<SelectionActivationRequest> ResolvePendingSelectionAsync()
    {
        var request = pendingSelection ?? new SelectionActivationRequest(null, 0, 0);
        if (!string.IsNullOrWhiteSpace(request.Text))
        {
            return request;
        }

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var selection = await selectionCaptureService.TryCaptureSelectionSnapshotAsync(CancellationToken.None);
            var text = selection?.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return request with { Text = text };
            }

            if (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(80));
            }
        }

        return request;
    }
}
