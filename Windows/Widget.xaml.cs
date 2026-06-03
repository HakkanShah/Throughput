using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Throughput.Helpers;

namespace Throughput.Windows;

/// <summary>
/// The Throughput widget - a glass pill showing live network speed plus CPU
/// and RAM usage. The user can resize it freely:
/// <list type="bullet">
///   <item>Above the reflow threshold: single-row layout that scales with size.</item>
///   <item>Below the reflow threshold: switches to a compact two-row layout.</item>
/// </list>
/// Single click drags; double-click opens the dashboard.
/// </summary>
public partial class Widget : Window
{
    /// <summary>
    /// Width below which the layout switches from single-row to two-row.
    /// Picked so the single-row content stops just before it would clip.
    /// </summary>
    private const double NarrowThresholdWidth = 210;

    private readonly DispatcherTimer _updateTimer;
    private readonly DispatcherTimer _sizePersistTimer;
    private readonly WarningGlow _warningGlow;
    // Re-raises the widget above the taskbar the instant anything else (the
    // taskbar, a launching app, alt-tab) comes to the foreground.
    private TopmostGuard? _topmostGuard;
    private System.Windows.Point _dragStartPosition;
    private bool _useNarrowLayout;
    // Tick counter - drives the periodic (cheap) topmost safety re-assertion.
    private int _tickCounter;

    public Widget()
    {
        InitializeComponent();

        // Drives the animated yellow/red snake outline when CPU or RAM runs high.
        // The snake reads its size from MainBorder so resizing keeps it in sync.
        _warningGlow = new WarningGlow(SnakeOutline, SnakeGlow, MainBorder);

        // Live readouts tick every second
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _updateTimer.Tick += UpdateTimer_Tick;

        // Debounce size persistence so we don't write settings on every resize tick
        _sizePersistTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _sizePersistTimer.Tick += (s, e) =>
        {
            _sizePersistTimer.Stop();
            App.Settings.SaveWidgetSize(Width, Height);
        };

        // Restore saved size + position before showing
        RestoreSize();
        PositionWindow();

        Loaded += (s, e) =>
        {
            _updateTimer.Start();
            ApplyLayoutForWidth(); // pick correct layout for the initial width

            // The window handle exists by Loaded, so the foreground hook can
            // attach. This - not the old Deactivated toggle - is what keeps the
            // widget above the taskbar when the user clicks it or opens an app.
            _topmostGuard = new TopmostGuard(this);
        };
        Closing += (s, e) =>
        {
            _updateTimer.Stop();
            _sizePersistTimer.Stop();
            _topmostGuard?.Dispose();
            _topmostGuard = null;
        };

        // Prevent minimizing - always restore if minimized
        StateChanged += (s, e) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
        };
    }

    /// <summary>
    /// Updates the live readouts and ensures the window stays visible.
    /// Writes to both layouts' TextBlocks so a layout swap shows fresh data instantly.
    /// </summary>
    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        var monitor = App.NetworkMonitor;
        if (monitor != null)
        {
            var (download, upload) = monitor.GetCurrentSpeed();
            var dl = SpeedFormatter.FormatBytesPerSecond(download);
            var ul = SpeedFormatter.FormatBytesPerSecond(upload);

            DownloadSpeedText.Text = dl;
            UploadSpeedText.Text = ul;
            DownloadSpeedTextN.Text = dl;
            UploadSpeedTextN.Text = ul;
        }

        var systemMonitor = App.SystemMonitor;
        if (systemMonitor != null)
        {
            var (cpuPercent, memoryPercent, _, _) = systemMonitor.GetUsage();
            var cpu = $"{cpuPercent:F0}%";
            var ram = $"{memoryPercent:F0}%";

            CpuText.Text = cpu;
            RamText.Text = ram;
            CpuTextN.Text = cpu;
            RamTextN.Text = ram;

            // Per-metric glow on whichever layout is active
            WarningGlow.StylePercentage(CpuText, CpuTextGlow, cpuPercent);
            WarningGlow.StylePercentage(RamText, RamTextGlow, memoryPercent);
            WarningGlow.StylePercentage(CpuTextN, CpuTextGlowN, cpuPercent);
            WarningGlow.StylePercentage(RamTextN, RamTextGlowN, memoryPercent);

            // Snake outline keyed to the higher of the two
            _warningGlow.Update(cpuPercent, memoryPercent);
        }

        // Re-assert visible+topmost only periodically (every ~30s), not every tick.
        // The TopmostGuard foreground hook handles the common cases instantly, so
        // this is purely a safety net for rare OS-level demotions. Skip it when the
        // widget is hidden (from the tray) so we don't force a hidden widget back.
        if (++_tickCounter % 30 == 0 && IsVisible)
        {
            WindowHelper.ForceVisibleAndTopmost(this);
        }
    }

    /// <summary>
    /// Switches between the single-row and two-row layout based on current width.
    /// </summary>
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyLayoutForWidth();

        // Debounce size persistence so we save once the user stops resizing
        _sizePersistTimer.Stop();
        _sizePersistTimer.Start();
    }

    /// <summary>
    /// Chooses the single-row or two-row layout based on the current width.
    /// </summary>
    private void ApplyLayoutForWidth()
    {
        bool narrow = ActualWidth > 0 && ActualWidth < NarrowThresholdWidth;
        if (narrow == _useNarrowLayout && WideLayout.IsLoaded) return;

        _useNarrowLayout = narrow;
        WideLayout.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
        NarrowLayout.Visibility = narrow ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Restores the user's saved widget size, if they've resized it before.
    /// </summary>
    private void RestoreSize()
    {
        var saved = App.Settings.GetWidgetSize();
        if (saved == null) return;

        // Clamp to the XAML-declared min/max so an old saved value can't push us
        // out of legal range.
        Width = Math.Clamp(saved.Width, MinWidth, MaxWidth);
        Height = Math.Clamp(saved.Height, MinHeight, MaxHeight);
    }

    /// <summary>
    /// Positions the window - restores the saved position or uses the default.
    /// </summary>
    private void PositionWindow()
    {
        var savedPosition = App.Settings.GetWidgetPosition();

        if (savedPosition != null)
        {
            int w = (int)Width;
            int h = (int)Height;

            if (PositionHelper.IsPositionVisible(savedPosition.Left, savedPosition.Top, w, h))
            {
                Left = savedPosition.Left;
                Top = savedPosition.Top;
                return;
            }

            var (clampedLeft, clampedTop) = PositionHelper.ClampToScreen(
                savedPosition.Left, savedPosition.Top, w, h);
            Left = clampedLeft;
            Top = clampedTop;
            return;
        }

        // Default position - bottom-right corner
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;
    }

    /// <summary>
    /// Saves the current widget position.
    /// </summary>
    private void SavePosition()
    {
        App.Settings.SaveWidgetPosition(Left, Top);
    }

    /// <summary>
    /// Shows the close button, resize grips, and brightens the border on hover.
    /// </summary>
    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        CloseButton.Visibility = Visibility.Visible;
        ResizeGripBR.Visibility = Visibility.Visible;
        ResizeGripBL.Visibility = Visibility.Visible;
        MainBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));
    }

    /// <summary>
    /// Hides the close button, resize grips, and restores the default border on leave.
    /// </summary>
    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        CloseButton.Visibility = Visibility.Hidden;
        ResizeGripBR.Visibility = Visibility.Hidden;
        ResizeGripBL.Visibility = Visibility.Hidden;
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF), 0));
        brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF), 1));
        MainBorder.BorderBrush = brush;
    }

    // ---- Resize grips (custom because AllowsTransparency disables native hit-test) ----

    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HT_BOTTOMLEFT = 16;
    private const int HT_BOTTOMRIGHT = 17;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private void ResizeGripBR_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        StartNativeResize(HT_BOTTOMRIGHT);
        e.Handled = true;
    }

    private void ResizeGripBL_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        StartNativeResize(HT_BOTTOMLEFT);
        e.Handled = true;
    }

    /// <summary>
    /// Hands the resize operation off to the OS so the user can drag the corner.
    /// </summary>
    private void StartNativeResize(int hitTest)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        SendMessage(hwnd, WM_NCLBUTTONDOWN, new IntPtr(hitTest), IntPtr.Zero);
    }

    /// <summary>
    /// Single click = drag the widget (never opens the app, even on a tap with
    /// no movement). Double-click = open the dashboard. This avoids the touchpad
    /// problem where every tap-to-grab was accidentally opening the main window.
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // A double-click on the widget opens the dashboard.
        if (e.ClickCount == 2)
        {
            App.ShowMainWindow();
            e.Handled = true;
            return;
        }

        // Single click - drag only. Tap-with-no-movement is just a no-op.
        _dragStartPosition = new System.Windows.Point(Left, Top);
        DragMove();

        if (Math.Abs(Left - _dragStartPosition.X) > 5 || Math.Abs(Top - _dragStartPosition.Y) > 5)
        {
            SavePosition();
        }
    }

    /// <summary>
    /// Closes only the widget - the app stays running in the tray.
    /// </summary>
    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        App.HideWidget();
    }
}
