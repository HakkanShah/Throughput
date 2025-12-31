using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Throughput.Helpers;

namespace Throughput.Windows;

/// <summary>
/// Minimal widget showing only download speed
/// Purely transparent, system-icon sized, close button on hover
/// </summary>
public partial class MinimalWidget : Window
{
    private readonly DispatcherTimer _updateTimer;

    public MinimalWidget()
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

        var (download, _) = monitor.GetCurrentSpeed();

        // Show only the speed value without the label (e.g., "12 MB/s")
        DownloadSpeedText.Text = SpeedFormatter.FormatBytesPerSecond(download);
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
        MainBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x60, 0x00, 0x00, 0x00));
    }

    /// <summary>
    /// Hides close button and border when not hovering
    /// </summary>
    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        CloseButton.Visibility = Visibility.Hidden;
        MainBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;
        MainBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0x00, 0x00, 0x00));
    }

    /// <summary>
    /// Allows dragging the window
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
