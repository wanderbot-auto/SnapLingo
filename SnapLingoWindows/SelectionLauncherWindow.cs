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
    private const int PanelWidth = 248;
    private const int PanelHeight = 46;
    private const int PanelCornerRadius = 14;
    private const int OffsetX = 16;
    private const int OffsetY = 12;
    private static readonly TimeSpan AutoHideDelay = TimeSpan.FromSeconds(10);

    private readonly AppWindow appWindow;
    private readonly nint hwnd;
    private readonly DispatcherQueueTimer autoHideTimer;
    private readonly Grid root;
    private readonly Action<TranslationMode> onInvoked;
    private readonly Action onDismissed;
    private bool hasPointerEntered;
    private bool isDismissPending;
    private bool isClosed;

    public bool IsClosed => isClosed;

    public SelectionLauncherWindow(
        LocalizationService localizer,
        Action<TranslationMode> onInvoked,
        Action onDismissed)
    {
        this.onInvoked = onInvoked;
        this.onDismissed = onDismissed;

        root = new Grid
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xF6, 0xF1, 0xEA)),
            Width = PanelWidth,
            Height = PanelHeight,
            Children =
            {
                BuildLauncherPanel(localizer),
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
            PanelWidth,
            PanelHeight,
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
        appWindow.Resize(new Windows.Graphics.SizeInt32(PanelWidth, PanelHeight));
        NativeWindowStyler.ApplyOverlayToolStyle(hwnd);
        ApplyLauncherRegion();

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }
    }

    private void ApplyLauncherRegion()
    {
        var regionHandle = NativeMethods.CreateRoundRectRgn(
            0,
            0,
            PanelWidth,
            PanelHeight,
            PanelCornerRadius * 2,
            PanelCornerRadius * 2);
        if (regionHandle == 0)
        {
            return;
        }

        try
        {
            if (NativeMethods.SetWindowRgn(hwnd, regionHandle, true) != 0)
            {
                regionHandle = 0;
            }
        }
        finally
        {
            if (regionHandle != 0)
            {
                NativeMethods.DeleteObject(regionHandle);
            }
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
            var maxX = workArea.X + workArea.Width - PanelWidth - 8;
            var maxY = workArea.Y + workArea.Height - PanelHeight - 8;

            var targetX = Math.Clamp(desiredX, minX, maxX);
            var targetY = Math.Clamp(desiredY, minY, maxY);
            appWindow.Move(new Windows.Graphics.PointInt32(targetX, targetY));
        }
        catch (COMException)
        {
            // Ignore transient positioning races and keep the launcher usable.
        }
    }

    private Border BuildLauncherPanel(LocalizationService localizer)
    {
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                BuildActionButton(localizer.Get("mode_translate"), TranslationMode.Translate),
                BuildActionButton(localizer.Get("mode_polish"), TranslationMode.Polish),
                BuildActionButton(localizer.Get("mode_continue"), TranslationMode.Continue),
            },
        };

        var layout = new Grid
        {
            Padding = new Thickness(10, 7, 10, 7),
            ColumnSpacing = 10,
        };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var logoHost = new Border
        {
            Width = 24,
            Height = 24,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x55, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(8),
            Child = new Image
            {
                Width = 16,
                Height = 16,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Source = new BitmapImage(new Uri("ms-appx:///Assets/GeneratedLogo/logo-24.png")),
                IsHitTestVisible = false,
            },
        };
        Grid.SetColumn(logoHost, 0);

        var divider = new Border
        {
            Width = 1,
            Margin = new Thickness(0, 4, 0, 4),
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x1A, 0x2C, 0x1C, 0x12)),
        };
        Grid.SetColumn(divider, 1);

        Grid.SetColumn(buttonRow, 2);

        layout.Children.Add(logoHost);
        layout.Children.Add(divider);
        layout.Children.Add(buttonRow);

        return new Border
        {
            Width = PanelWidth,
            Height = PanelHeight,
            CornerRadius = new CornerRadius(PanelCornerRadius),
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xF6, 0xF1, 0xEA)),
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x1F, 0x31, 0x1D, 0x12)),
            BorderThickness = new Thickness(1),
            Child = layout,
        };
    }

    private Button BuildActionButton(string label, TranslationMode mode)
    {
        var button = new Button
        {
            MinWidth = 58,
            Height = 30,
            Padding = new Thickness(12, 0, 12, 0),
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xE8, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x1A, 0x2C, 0x1C, 0x12)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Content = new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x2E, 0x24, 0x1F)),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            },
            Tag = mode,
        };
        button.Click += OnActionButtonClicked;
        return button;
    }

    private void OnActionButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TranslationMode mode })
        {
            return;
        }

        StopAutoHideTimer();
        onInvoked(mode);
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
        root.PointerEntered -= OnPointerEntered;
        root.PointerExited -= OnPointerExited;
        Closed -= OnClosed;
    }
}
