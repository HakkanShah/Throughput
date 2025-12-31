using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Throughput.Helpers;

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

        // Position window
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
    /// Positions window at bottom-right, above taskbar
    /// </summary>
    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 10;
        Top = workArea.Bottom - Height - 10;
    }

    /// <summary>
    /// Shows close button and border on hover
    /// </summary>
    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        CloseButton.Visibility = Visibility.Visible;
        MainBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
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

