using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Throughput.Helpers;

namespace Throughput.Windows;

/// <summary>
/// Full overlay window displaying live network throughput
/// Always-on-top, with header and "More Details" link
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _updateTimer;

    public OverlayWindow()
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
        Closing += OverlayWindow_Closing;
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
    /// Allows dragging the window
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
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

