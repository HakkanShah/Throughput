using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using Throughput.Services;
using Application = System.Windows.Application;

namespace Throughput;

/// <summary>
/// Main overlay window displaying network speed
/// </summary>
public partial class MainWindow : Window
{
    private readonly NetworkSpeedMonitor _networkMonitor;
    private readonly SpeedTestService _speedTestService;
    private readonly DispatcherTimer _updateTimer;
    private readonly NotifyIcon _trayIcon;
    private CancellationTokenSource? _speedTestCts;

    public MainWindow()
    {
        InitializeComponent();
        
        // Initialize services
        _networkMonitor = new NetworkSpeedMonitor();
        _speedTestService = new SpeedTestService();
        
        // Subscribe to speed test events
        _speedTestService.ProgressChanged += OnSpeedTestProgress;
        _speedTestService.TestCompleted += OnSpeedTestCompleted;
        
        // Set up update timer (1 second interval)
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        
        // Set up system tray icon
        _trayIcon = CreateTrayIcon();
        
        // Position window
        PositionWindow();
        
        // Start monitoring
        Loaded += (s, e) => _updateTimer.Start();
        Closing += MainWindow_Closing;
    }

    /// <summary>
    /// Updates the speed display
    /// </summary>
    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        var (download, upload) = _networkMonitor.GetCurrentSpeed();
        
        DownloadSpeedText.Text = FormatSpeed(download);
        UploadSpeedText.Text = FormatSpeed(upload);
    }

    /// <summary>
    /// Formats bytes per second to human readable format
    /// </summary>
    private static string FormatSpeed(double bytesPerSecond)
    {
        string[] units = ["B/s", "KB/s", "MB/s", "GB/s"];
        int unitIndex = 0;
        double speed = bytesPerSecond;

        while (speed >= 1024 && unitIndex < units.Length - 1)
        {
            speed /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 
            ? $"{speed:F0} {units[unitIndex]}" 
            : $"{speed:F1} {units[unitIndex]}";
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
    /// Creates the system tray icon with context menu
    /// </summary>
    private NotifyIcon CreateTrayIcon()
    {
        var icon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "Throughput - Network Speed Monitor"
        };

        // Create context menu
        var contextMenu = new ContextMenuStrip();
        
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitApplication();
        
        contextMenu.Items.Add(exitItem);
        icon.ContextMenuStrip = contextMenu;
        
        // Double-click to show/focus window
        icon.DoubleClick += (s, e) =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        };

        return icon;
    }

    /// <summary>
    /// Loads the tray icon (uses system default)
    /// </summary>
    private static Icon LoadTrayIcon()
    {
        return SystemIcons.Application;
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
        ExitApplication();
    }

    /// <summary>
    /// Handles window closing - cleanup resources
    /// </summary>
    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _speedTestCts?.Cancel();
        _updateTimer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _networkMonitor.Dispose();
        _speedTestService.Dispose();
    }

    /// <summary>
    /// Exits the application
    /// </summary>
    private void ExitApplication()
    {
        _speedTestCts?.Cancel();
        _updateTimer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _networkMonitor.Dispose();
        _speedTestService.Dispose();
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Handles click on speed test button
    /// </summary>
    private async void SpeedTest_Click(object sender, MouseButtonEventArgs e)
    {
        // Prevent drag when clicking the button
        e.Handled = true;
        
        if (_speedTestService.IsRunning) return;
        
        SpeedTestText.Text = "Testing...";
        SpeedTestSubtext.Text = "Measuring speed";
        
        _speedTestCts = new CancellationTokenSource();
        await _speedTestService.RunTestAsync(_speedTestCts.Token);
    }

    /// <summary>
    /// Updates UI with speed test progress
    /// </summary>
    private void OnSpeedTestProgress(double speedMbps)
    {
        Dispatcher.Invoke(() =>
        {
            SpeedTestText.Text = $"{speedMbps:F1} Mbps";
            SpeedTestSubtext.Text = "Testing...";
        });
    }

    /// <summary>
    /// Updates UI when speed test completes
    /// </summary>
    private void OnSpeedTestCompleted(SpeedTestResult result)
    {
        Dispatcher.Invoke(() =>
        {
            if (result.Success)
            {
                SpeedTestText.Text = $"{result.DownloadSpeedMbps:F1} Mbps";
                SpeedTestSubtext.Text = "Click to retest";
            }
            else
            {
                SpeedTestText.Text = "Test failed";
                SpeedTestSubtext.Text = "Click to retry";
            }
        });
    }
}
