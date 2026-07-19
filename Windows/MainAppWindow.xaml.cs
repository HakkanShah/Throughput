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
    private bool? _lastWidgetVisible;

    // Starts true so slider ValueChanged notifications fired while
    // InitializeComponent is still parsing (before every named field is
    // assigned) are ignored instead of touching a sibling that isn't set yet.
    private bool _loadingAnimationSettings = true;

    public MainAppWindow()
    {
        InitializeComponent();
        LoadAnimationSettings();
        _loadingAnimationSettings = false;

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

        Loaded += (s, e) =>
        {
            _updateTimer.Start();
            UpdateWidgetStatus();
        };
        Closing += MainAppWindow_Closing;
    }

    /// <summary>
    /// Applies the acrylic frosted-glass backdrop once the native window handle exists.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        AcrylicGlass.Enable(this);
    }

    /// <summary>
    /// Updates the Launch Widget button + status text to reflect whether the
    /// widget is currently visible.
    /// </summary>
    private void UpdateWidgetStatus()
    {
        bool visible = App.IsWidgetVisible;

        // Only re-skin when the state flips; this runs every second.
        if (_lastWidgetVisible == visible) return;
        _lastWidgetVisible = visible;

        LaunchWidgetButton.Content = visible ? "Hide Widget" : "Launch Widget";
        LaunchWidgetButton.Style = (Style)FindResource(visible ? "SecondaryButton" : "PrimaryButton");
        WidgetStatusText.Text = visible ? "Visible on desktop" : "Hidden";
        WidgetStatusDot.Fill = visible
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3D, 0xDC, 0xA4)) // green
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x89, 0x92, 0xA6)); // dim
    }

    /// <summary>
    /// Updates the live throughput readouts and keeps the Launch Widget button
    /// in sync with the widget's actual visibility.
    /// </summary>
    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        UpdateWidgetStatus();

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

            // The test allocates transient upload/download buffers; reclaim them.
            MemoryTrimmer.Trim();
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
        // Dashboard is the heaviest window; release its pages while it's closed.
        MemoryTrimmer.Trim();
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
    /// Toggles the desktop widget: hides it when visible, shows it when hidden.
    /// </summary>
    private void LaunchWidgetButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.IsWidgetVisible)
            App.HideWidget();
        else
            App.ShowWidget();

        UpdateWidgetStatus();
    }

    // ---- Border warning animation settings ----

    /// <summary>
    /// Populates the animation controls from the currently saved settings.
    /// </summary>
    private void LoadAnimationSettings()
    {
        var animation = App.Settings.Animation;
        AnimationEnabledCheckBox.IsChecked = animation.Enabled;
        WarningSlider.Value = animation.WarningThreshold;
        CriticalSlider.Value = animation.CriticalThreshold;
        WarningSlider.IsEnabled = animation.Enabled;
        CriticalSlider.IsEnabled = animation.Enabled;
        UpdateAnimationLabels();
    }

    /// <summary>
    /// Keeps the critical slider from dropping to or below the warning slider.
    /// </summary>
    private void WarningSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loadingAnimationSettings) return;

        if (CriticalSlider.Value <= WarningSlider.Value)
        {
            CriticalSlider.Value = Math.Min(CriticalSlider.Maximum, WarningSlider.Value + 1);
        }
        UpdateAnimationLabels();
    }

    /// <summary>
    /// Keeps the warning slider from rising to or above the critical slider.
    /// </summary>
    private void CriticalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loadingAnimationSettings) return;

        if (WarningSlider.Value >= CriticalSlider.Value)
        {
            WarningSlider.Value = Math.Max(WarningSlider.Minimum, CriticalSlider.Value - 1);
        }
        UpdateAnimationLabels();
    }

    private void UpdateAnimationLabels()
    {
        WarningValueText.Text = $"{WarningSlider.Value:F0}%";
        CriticalValueText.Text = $"{CriticalSlider.Value:F0}%";
    }

    private void AnimationEnabledCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingAnimationSettings) return;

        bool enabled = AnimationEnabledCheckBox.IsChecked == true;
        WarningSlider.IsEnabled = enabled;
        CriticalSlider.IsEnabled = enabled;
    }

    /// <summary>
    /// Persists the chosen animation settings and applies them to the live widget immediately.
    /// </summary>
    private void SaveAnimationButton_Click(object sender, RoutedEventArgs e)
    {
        bool enabled = AnimationEnabledCheckBox.IsChecked == true;
        App.Settings.SaveAnimationSettings(enabled, WarningSlider.Value, CriticalSlider.Value);

        WarningGlow.AnimationEnabled = App.Settings.Animation.Enabled;
        WarningGlow.WarningThreshold = App.Settings.Animation.WarningThreshold;
        WarningGlow.CriticalThreshold = App.Settings.Animation.CriticalThreshold;

        AnimationSavedText.Text = "Saved";
    }
}

