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
    }

    /// <summary>
    /// Updates the speed display
    /// </summary>
    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        var monitor = App.NetworkMonitor;
        if (monitor == null) return;

        var (download, upload) = monitor.GetCurrentSpeed();

        DownloadSpeedText.Text = SpeedFormatter.FormatBytesPerSecond(download);
        UploadSpeedText.Text = SpeedFormatter.FormatBytesPerSecond(upload);
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
    /// Allows dragging the window, or opens dashboard on click
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    /// <summary>
    /// Opens dashboard when clicking anywhere (handled via PreviewMouseLeftButtonUp)
    /// </summary>
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        
        // If we didn't drag much, treat it as a click to open dashboard
        if (!IsMouseCaptured)
        {
            App.ShowMainWindow();
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
}
