using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Throughput.Helpers;
using Throughput.Models;

namespace Throughput.Windows;

/// <summary>
/// Full overlay window displaying live network throughput
/// Always-on-top, with header and "More Details" link
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _updateTimer;
    private System.Windows.Point _dragStartPosition;
    private bool _isDragging;

    public OverlayWindow()
    {
        InitializeComponent();

        // Set up update timer (1 second interval)
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        // Position window (restores saved position or uses default)
        PositionWindow();

        // Start monitoring
        Loaded += (s, e) => _updateTimer.Start();
        Closing += OverlayWindow_Closing;
        
        // Re-apply topmost when deactivated (fixes taskbar click issue)
        Deactivated += (s, e) =>
        {
            Topmost = false;
            Topmost = true;
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
    /// Updates the speed display and ensures window stays visible
    /// </summary>
    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        var monitor = App.NetworkMonitor;
        if (monitor == null) return;

        var (download, upload) = monitor.GetCurrentSpeed();

        DownloadSpeedText.Text = SpeedFormatter.FormatBytesPerSecond(download);
        UploadSpeedText.Text = SpeedFormatter.FormatBytesPerSecond(upload);
        
        // Force window to stay visible and topmost using Windows API
        Helpers.WindowHelper.ForceVisibleAndTopmost(this);
    }

    /// <summary>
    /// Positions window - restores saved position or uses default
    /// </summary>
    private void PositionWindow()
    {
        // Try to restore saved position
        var savedPosition = App.Settings.GetWidgetPosition(WidgetType.Full);
        
        if (savedPosition != null)
        {
            // Validate the position is still visible on screen
            var estimatedWidth = 200;  // Approximate width for SizeToContent
            var estimatedHeight = 100; // Approximate height for SizeToContent
            
            if (PositionHelper.IsPositionVisible(savedPosition.Left, savedPosition.Top, estimatedWidth, estimatedHeight))
            {
                Left = savedPosition.Left;
                Top = savedPosition.Top;
                return;
            }
            
            // Position is off-screen, clamp to nearest visible area
            var (clampedLeft, clampedTop) = PositionHelper.ClampToScreen(
                savedPosition.Left, savedPosition.Top, estimatedWidth, estimatedHeight);
            Left = clampedLeft;
            Top = clampedTop;
            return;
        }

        // Use default position (bottom-right corner)
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - 210;
        Top = workArea.Bottom - 110;
    }

    /// <summary>
    /// Saves the current widget position
    /// </summary>
    private void SavePosition()
    {
        App.Settings.SaveWidgetPosition(WidgetType.Full, Left, Top);
    }

    /// <summary>
    /// Allows dragging the window and saves position after drag
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            _dragStartPosition = new System.Windows.Point(Left, Top);
            _isDragging = false;
            
            DragMove();
            
            // Check if we actually moved
            if (Math.Abs(Left - _dragStartPosition.X) > 5 || Math.Abs(Top - _dragStartPosition.Y) > 5)
            {
                _isDragging = true;
                // Save the new position
                SavePosition();
            }
        }
    }

    /// <summary>
    /// Handles close button click
    /// </summary>
    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        App.ExitApplication();
    }

    /// <summary>
    /// Opens the main dashboard window
    /// </summary>
    private void OpenDashboard_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        App.ShowMainWindow();
    }

    /// <summary>
    /// Handles window closing - cleanup resources
    /// </summary>
    private void OverlayWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _updateTimer.Stop();
    }
}
