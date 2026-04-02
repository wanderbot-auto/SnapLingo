using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using SnapLingoWindows.Models;

namespace SnapLingoWindows;

public partial class App : Application
{
    private AutoSelectionMonitorService? autoSelectionMonitor;
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
        autoSelectionMonitor = new AutoSelectionMonitorService(
            DispatcherQueue.GetForCurrentThread()!,
            new ClipboardSelectionCaptureService(),
            OnAutoSelectionDetectedAsync);
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
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Task.CompletedTask;
        }

        pendingSelection = request;
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
            SelectionLauncher = new SelectionLauncherWindow(OnSelectionLauncherInvoked, OnSelectionLauncherDismissed);
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
            SelectionLauncher = new SelectionLauncherWindow(OnSelectionLauncherInvoked, OnSelectionLauncherDismissed);
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

    private async void OnSelectionLauncherInvoked()
    {
        if (pendingSelection is null)
        {
            HideSelectionLauncher();
            return;
        }

        var request = pendingSelection;
        pendingSelection = null;
        HideSelectionLauncher();
        await ViewModel.HandleSelectionLauncherAsync(request.Text);
    }

    private void OnSelectionLauncherDismissed()
    {
        pendingSelection = null;
        HideSelectionLauncher();
    }
}
