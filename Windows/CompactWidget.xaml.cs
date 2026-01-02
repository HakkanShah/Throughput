using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Throughput.Helpers;
using Throughput.Models;

namespace Throughput.Windows;

/// <summary>
/// Compact widget showing only download and upload speeds
/// Transparent background, close button on hover, click to open dashboard
/// </summary>
public partial class CompactWidget : Window
{
    private readonly DispatcherTimer _updateTimer;
    private System.Windows.Point _dragStartPosition;
    private bool _isDragging;

    public CompactWidget()
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
        Closing += (s, e) => _updateTimer.Stop();
        
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
        WindowHelper.ForceVisibleAndTopmost(this);
    }

    /// <summary>
    /// Positions window - restores saved position or uses default
    /// </summary>
    private void PositionWindow()
    {
        // Try to restore saved position
        var savedPosition = App.Settings.GetWidgetPosition(WidgetType.Compact);
        
        if (savedPosition != null)
        {
            // Validate the position is still visible on screen
            var estimatedWidth = 150;  // Approximate width for SizeToContent
            var estimatedHeight = 40;  // Approximate height for SizeToContent
            
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
        Left = workArea.Right - 160;
        Top = workArea.Bottom - 50;
    }

    /// <summary>
    /// Saves the current widget position
    /// </summary>
    private void SavePosition()
    {
        App.Settings.SaveWidgetPosition(WidgetType.Compact, Left, Top);
    }

    /// <summary>
    /// Shows close button and border on hover
    /// </summary>
    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        CloseButton.Visibility = Visibility.Visible;
        MainBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
    }

    /// <summary>
    /// Hides close button and border when not hovering
    /// </summary>
    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        CloseButton.Visibility = Visibility.Hidden;
        MainBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
    }

    /// <summary>
    /// Tracks start position and initiates drag
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPosition = new System.Windows.Point(Left, Top);
        _isDragging = false;
        
        if (e.ClickCount == 1)
        {
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
    /// Opens dashboard only if it was a click (not a drag)
    /// </summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        
        // Only open dashboard if we didn't drag
        if (!_isDragging)
        {
            App.ShowMainWindow();
        }
        _isDragging = false;
    }

    /// <summary>
    /// Handles close button click
    /// </summary>
    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        App.ExitApplication();
    }
}
