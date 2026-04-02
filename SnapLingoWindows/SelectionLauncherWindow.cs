using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SnapLingoWindows.Models;
using SnapLingoWindows.Services;
using WinRT.Interop;

namespace SnapLingoWindows;

public sealed class SelectionLauncherWindow : Window
{
    private const int LauncherSize = 40;
    private const int OffsetX = 14;
    private const int OffsetY = 10;
    private static readonly TimeSpan AutoHideDelay = TimeSpan.FromSeconds(10);

    private readonly AppWindow appWindow;
    private readonly nint hwnd;
    private readonly DispatcherQueueTimer autoHideTimer;
    private readonly Button launcherButton;
    private readonly Grid root;
    private readonly Action onInvoked;
    private readonly Action onDismissed;
    private bool hasPointerEntered;
    private bool isDismissPending;
    private bool isClosed;

    public bool IsClosed => isClosed;

    public SelectionLauncherWindow(Action onInvoked, Action onDismissed)
    {
        this.onInvoked = onInvoked;
        this.onDismissed = onDismissed;

        launcherButton = BuildLauncherButton();
        root = new Grid
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x00, 0x00, 0x00, 0x00)),
            Width = LauncherSize,
            Height = LauncherSize,
            Children =
            {
                launcherButton,
            },
        };
        root.PointerEntered += OnPointerEntered;
        root.PointerExited += OnPointerExited;
        Content = root;

        Title = "SnapLingo Launcher";
        hwnd = WindowNative.GetWindowHandle(this);
        appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd));
        autoHideTimer = (DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("Selection launcher requires a dispatcher queue."))
            .CreateTimer();
        autoHideTimer.Interval = AutoHideDelay;
        autoHideTimer.IsRepeating = false;
        autoHideTimer.Tick += OnAutoHideTimerTick;

        ConfigureWindow();
        Closed += OnClosed;
    }

    public void Present(SelectionActivationRequest request)
    {
        ResetDismissState();
        PositionNear(request.AnchorX, request.AnchorY);
        Activate();
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            appWindow.Position.X,
            appWindow.Position.Y,
            LauncherSize,
            LauncherSize,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        RestartAutoHideTimer();
    }

    public void HideLauncher()
    {
        StopAutoHideTimer();
        ResetDismissState();
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
    }

    private void ConfigureWindow()
    {
        appWindow.Resize(new Windows.Graphics.SizeInt32(LauncherSize, LauncherSize));
        NativeWindowStyler.ApplyOverlayToolStyle(hwnd);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }
    }

    private void PositionNear(int anchorX, int anchorY)
    {
        try
        {
            var displayArea = DisplayArea.GetFromPoint(
                new Windows.Graphics.PointInt32(anchorX, anchorY),
                DisplayAreaFallback.Nearest);
            var workArea = displayArea.WorkArea;
            var desiredX = anchorX + OffsetX;
            var desiredY = anchorY + OffsetY;

            var minX = workArea.X + 8;
            var minY = workArea.Y + 8;
            var maxX = workArea.X + workArea.Width - LauncherSize - 8;
            var maxY = workArea.Y + workArea.Height - LauncherSize - 8;

            var targetX = Math.Clamp(desiredX, minX, maxX);
            var targetY = Math.Clamp(desiredY, minY, maxY);
            appWindow.Move(new Windows.Graphics.PointInt32(targetX, targetY));
        }
        catch (COMException)
        {
            // Ignore transient positioning races and keep the launcher usable.
        }
    }

    private Button BuildLauncherButton()
    {
        var image = new Image
        {
            Width = 22,
            Height = 22,
            Source = new BitmapImage(new Uri("ms-appx:///Assets/Square44x44Logo.targetsize-24_altform-unplated.png")),
        };

        var button = new Button
        {
            Width = LauncherSize,
            Height = LauncherSize,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xF5, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x33, 0x15, 0x5D, 0xFC)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(LauncherSize / 2.0),
            Content = image,
        };
        button.Click += OnLauncherClicked;
        return button;
    }

    private void OnLauncherClicked(object sender, RoutedEventArgs e)
    {
        StopAutoHideTimer();
        onInvoked();
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        hasPointerEntered = true;
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!hasPointerEntered)
        {
            return;
        }

        RequestDismiss();
    }

    private void OnAutoHideTimerTick(DispatcherQueueTimer sender, object args)
    {
        RequestDismiss();
    }

    private void RequestDismiss()
    {
        if (isDismissPending)
        {
            return;
        }

        isDismissPending = true;
        StopAutoHideTimer();
        onDismissed();
    }

    private void RestartAutoHideTimer()
    {
        StopAutoHideTimer();
        autoHideTimer.Start();
    }

    private void StopAutoHideTimer()
    {
        if (autoHideTimer.IsRunning)
        {
            autoHideTimer.Stop();
        }
    }

    private void ResetDismissState()
    {
        hasPointerEntered = false;
        isDismissPending = false;
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        isClosed = true;
        StopAutoHideTimer();
        autoHideTimer.Tick -= OnAutoHideTimerTick;
        launcherButton.Click -= OnLauncherClicked;
        root.PointerEntered -= OnPointerEntered;
        root.PointerExited -= OnPointerExited;
        Closed -= OnClosed;
    }
}
