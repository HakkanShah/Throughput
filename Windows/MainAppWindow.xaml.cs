using System.Windows;
using System.Windows.Threading;
using Throughput.Helpers;
using Throughput.Models;
using Throughput.Services;

namespace Throughput.Windows;

/// <summary>
/// Main application dashboard window with speed test controls and detailed results
/// </summary>
public partial class MainAppWindow : Window
{
    private readonly DispatcherTimer _updateTimer;
    private CancellationTokenSource? _speedTestCts;
    private bool _isTestRunning;

    public MainAppWindow()
    {
        InitializeComponent();

        // Set up update timer for live throughput
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        // Subscribe to speed test events from the shared service
        var speedTestService = App.SpeedTestService;
        if (speedTestService != null)
        {
            speedTestService.ProgressChanged += OnSpeedTestProgress;
            speedTestService.TestCompleted += OnSpeedTestCompleted;
        }

        // Initialize widget selector
        InitializeWidgetSelector();

        Loaded += (s, e) => _updateTimer.Start();
        Closing += MainAppWindow_Closing;
    }

    /// <summary>
    /// Initializes the widget selector UI
    /// </summary>
    private void InitializeWidgetSelector()
    {
        // Set the current widget as selected
        UpdateWidgetSelectorUI();
    }

    /// <summary>
    /// Updates widget selector to reflect current selection
    /// </summary>
    private void UpdateWidgetSelectorUI()
    {
        var currentType = App.CurrentWidgetType;
        FullWidgetRadio.IsChecked = currentType == WidgetType.Full;
        CompactWidgetRadio.IsChecked = currentType == WidgetType.Compact;
        MinimalWidgetRadio.IsChecked = currentType == WidgetType.Minimal;
        SpeedTestWidgetRadio.IsChecked = currentType == WidgetType.SpeedTest;
    }

    /// <summary>
    /// Updates live throughput display
    /// </summary>
    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_isTestRunning) return;

        var monitor = App.NetworkMonitor;
        if (monitor == null) return;

        var (download, upload) = monitor.GetCurrentSpeed();

        LiveDownloadSpeed.Text = SpeedFormatter.FormatBytesPerSecond(download);
        LiveUploadSpeed.Text = SpeedFormatter.FormatBytesPerSecond(upload);
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

        // Dim live throughput during test
        LiveDownloadSpeed.Opacity = 0.5;
        LiveUploadSpeed.Opacity = 0.5;

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
            TestStatusText.Text = progress.StatusMessage;
            TestProgressBar.IsIndeterminate = false;
            TestProgressBar.Value = progress.ProgressPercent;

            // Show partial results as test progresses
            switch (progress.Phase)
            {
                case SpeedTestPhase.Latency:
                    TestStatusText.Text = $"Measuring latency... {progress.CurrentLatencyMs:F0}ms";
                    break;
                case SpeedTestPhase.Download:
                    TestStatusText.Text = $"Testing download... {progress.CurrentSpeedMbps:F1} Mbps";
                    break;
                case SpeedTestPhase.Upload:
                    TestStatusText.Text = $"Testing upload... {progress.CurrentSpeedMbps:F1} Mbps";
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
            SpeedTestButton.Content = "⚡ Test My Internet Speed";
            TestProgressBar.Visibility = Visibility.Collapsed;

            // Restore live throughput opacity
            LiveDownloadSpeed.Opacity = 1.0;
            LiveUploadSpeed.Opacity = 1.0;

            // Reset status text color
            TestStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x9c, 0xa3, 0xaf));

            if (result.Success)
            {
                TestStatusText.Text = $"✅ Test completed at {result.TestTimestamp:HH:mm:ss}";
                ResultsGrid.Visibility = Visibility.Visible;

                // Format download speed
                ResultDownload.Text = result.DownloadSpeedMbps >= 100
                    ? $"{result.DownloadSpeedMbps:F0}"
                    : $"{result.DownloadSpeedMbps:F1}";
                
                // Show MB/s equivalent for download
                double downloadMBs = result.DownloadSpeedMbps / 8;
                ResultDownloadMB.Text = $"≈ {downloadMBs:F1} MB/s";

                // Format upload speed
                ResultUpload.Text = result.UploadSpeedMbps >= 100
                    ? $"{result.UploadSpeedMbps:F0}"
                    : $"{result.UploadSpeedMbps:F1}";
                
                // Show MB/s equivalent for upload
                double uploadMBs = result.UploadSpeedMbps / 8;
                ResultUploadMB.Text = $"≈ {uploadMBs:F1} MB/s";

                // Format latency
                ResultLatency.Text = result.LatencyMs >= 10
                    ? $"{result.LatencyMs:F0}"
                    : $"{result.LatencyMs:F1}";
            }
            else
            {
                TestStatusText.Text = $"❌ {result.ErrorMessage ?? "Test failed - please try again"}";
                TestStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xef, 0x44, 0x44)); // Red
            }
        });
    }

    /// <summary>
    /// Allows dragging the window
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    /// <summary>
    /// Minimizes the window
    /// </summary>
    private void MinimizeButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        WindowState = WindowState.Minimized;
    }

    /// <summary>
    /// Closes/hides the window
    /// </summary>
    private void CloseButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        Hide();
    }

    /// <summary>
    /// Opens Hakkan's website
    /// </summary>
    private void OpenHakkanLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://hakkan.is-a.dev",
                UseShellExecute = true
            });
        }
        catch { }
    }

    /// <summary>
    /// Handles window closing
    /// </summary>
    private void MainAppWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Cancel any running test
        _speedTestCts?.Cancel();
        _updateTimer.Stop();

        // Unsubscribe from events
        var speedTestService = App.SpeedTestService;
        if (speedTestService != null)
        {
            speedTestService.ProgressChanged -= OnSpeedTestProgress;
            speedTestService.TestCompleted -= OnSpeedTestCompleted;
        }

        // Just hide the window, don't close the app
        e.Cancel = true;
        Hide();
    }

    /// <summary>
    /// Handles widget selection change
    /// </summary>
    private void WidgetRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton radio && radio.Tag is string tagStr)
        {
            if (Enum.TryParse<WidgetType>(tagStr, out var widgetType))
            {
                // Pass 'this' so the widget appears below the dashboard
                App.SwitchWidget(widgetType, saveAsDefault: true, positionBelowWindow: this);
            }
        }
    }
}

