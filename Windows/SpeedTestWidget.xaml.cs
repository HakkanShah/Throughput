using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Throughput.Helpers;
using Throughput.Models;

namespace Throughput.Windows;

/// <summary>
/// Speed test focused widget
/// Shows test button and results, click to open dashboard
/// </summary>
public partial class SpeedTestWidget : Window
{
    private CancellationTokenSource? _speedTestCts;
    private bool _isTestRunning;
    private readonly System.Windows.Threading.DispatcherTimer _visibilityTimer;
    private System.Windows.Point _dragStartPosition;
    private bool _isDragging;

    public SpeedTestWidget()
    {
        InitializeComponent();

        // Subscribe to speed test events from shared service
        var speedTestService = App.SpeedTestService;
        if (speedTestService != null)
        {
            speedTestService.ProgressChanged += OnSpeedTestProgress;
            speedTestService.TestCompleted += OnSpeedTestCompleted;
        }

        // Position window (restores saved position or uses default)
        PositionWindow();

        Closing += SpeedTestWidget_Closing;
        
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
        
        // Timer to ensure window stays visible using Windows API
        _visibilityTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _visibilityTimer.Tick += (s, e) =>
        {
            Helpers.WindowHelper.ForceVisibleAndTopmost(this);
        };
        
        Loaded += (s, e) => _visibilityTimer.Start();
    }

    /// <summary>
    /// Positions window - restores saved position or uses default
    /// </summary>
    private void PositionWindow()
    {
        // Try to restore saved position
        var savedPosition = App.Settings.GetWidgetPosition(WidgetType.SpeedTest);
        
        if (savedPosition != null)
        {
            // Validate the position is still visible on screen
            var estimatedWidth = 200;  // Approximate width for SizeToContent
            var estimatedHeight = 150; // Approximate height for SizeToContent
            
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
        Top = workArea.Bottom - 160;
    }

    /// <summary>
    /// Saves the current widget position
    /// </summary>
    private void SavePosition()
    {
        App.Settings.SaveWidgetPosition(WidgetType.SpeedTest, Left, Top);
    }

    /// <summary>
    /// Shows close button on hover
    /// </summary>
    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        CloseButton.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Hides close button when not hovering
    /// </summary>
    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        CloseButton.Visibility = Visibility.Hidden;
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
    /// Handles speed test button click
    /// </summary>
    private async void SpeedTestButton_Click(object sender, RoutedEventArgs e)
    {
        var speedTestService = App.SpeedTestService;
        if (speedTestService == null || speedTestService.IsRunning) return;

        _isTestRunning = true;
        SpeedTestButton.IsEnabled = false;
        SpeedTestButton.Content = "Testing...";
        TestProgressBar.Visibility = Visibility.Visible;
        TestProgressBar.IsIndeterminate = true;
        ResultsGrid.Visibility = Visibility.Collapsed;
        MoreDetailsLink.Visibility = Visibility.Collapsed;

        _speedTestCts = new CancellationTokenSource();
        await speedTestService.RunFullTestAsync(_speedTestCts.Token);
    }

    /// <summary>
    /// Updates UI during speed test progress
    /// </summary>
    private void OnSpeedTestProgress(SpeedTestProgress progress)
    {
        Dispatcher.Invoke(() =>
        {
            TestProgressBar.IsIndeterminate = false;
            TestProgressBar.Value = progress.ProgressPercent;

            switch (progress.Phase)
            {
                case SpeedTestPhase.Latency:
                    SpeedTestButton.Content = $"Testing... {progress.CurrentLatencyMs:F0}ms";
                    break;
                case SpeedTestPhase.Download:
                    SpeedTestButton.Content = $"↓ {progress.CurrentSpeedMbps:F1} Mbps";
                    break;
                case SpeedTestPhase.Upload:
                    SpeedTestButton.Content = $"↑ {progress.CurrentSpeedMbps:F1} Mbps";
                    break;
            }
        });
    }

    /// <summary>
    /// Updates UI when speed test completes
    /// </summary>
    private void OnSpeedTestCompleted(SpeedTestResult result)
    {
        Dispatcher.Invoke(() =>
        {
            _isTestRunning = false;
            SpeedTestButton.IsEnabled = true;
            SpeedTestButton.Content = "⚡ Test Speed";
            TestProgressBar.Visibility = Visibility.Collapsed;
            MoreDetailsLink.Visibility = Visibility.Visible;

            if (result.Success)
            {
                ResultsGrid.Visibility = Visibility.Visible;

                ResultDownload.Text = result.DownloadSpeedMbps >= 100
                    ? $"{result.DownloadSpeedMbps:F0}"
                    : $"{result.DownloadSpeedMbps:F1}";

                ResultUpload.Text = result.UploadSpeedMbps >= 100
                    ? $"{result.UploadSpeedMbps:F0}"
                    : $"{result.UploadSpeedMbps:F1}";

                ResultLatency.Text = result.LatencyMs >= 10
                    ? $"{result.LatencyMs:F0}"
                    : $"{result.LatencyMs:F1}";
            }
            else
            {
                SpeedTestButton.Content = "❌ Failed - Retry";
            }
        });
    }

    /// <summary>
    /// Handles window closing
    /// </summary>
    private void SpeedTestWidget_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _speedTestCts?.Cancel();
        _visibilityTimer.Stop();

        var speedTestService = App.SpeedTestService;
        if (speedTestService != null)
        {
            speedTestService.ProgressChanged -= OnSpeedTestProgress;
            speedTestService.TestCompleted -= OnSpeedTestCompleted;
        }
    }
}
