using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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

        // Position window
        PositionWindow();

        Closing += SpeedTestWidget_Closing;
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

        var speedTestService = App.SpeedTestService;
        if (speedTestService != null)
        {
            speedTestService.ProgressChanged -= OnSpeedTestProgress;
            speedTestService.TestCompleted -= OnSpeedTestCompleted;
        }
    }
}
