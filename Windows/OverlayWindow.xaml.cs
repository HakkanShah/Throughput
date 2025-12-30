using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using Throughput.Helpers;
using Throughput.Models;
using Throughput.Services;
using Application = System.Windows.Application;

namespace Throughput.Windows;

/// <summary>
/// Small overlay window displaying live network throughput
/// Always-on-top, minimal UI
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly NetworkSpeedMonitor _networkMonitor;
    private readonly SpeedTestService _speedTestService;
    private readonly DispatcherTimer _updateTimer;
    private readonly NotifyIcon _trayIcon;

    public OverlayWindow()
    {
        InitializeComponent();

        // Initialize services
        _networkMonitor = new NetworkSpeedMonitor();
        _speedTestService = new SpeedTestService();

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
        Closing += OverlayWindow_Closing;
    }

    /// <summary>
    /// Access to the speed test service for the main dashboard
    /// </summary>
    public SpeedTestService SpeedTestService => _speedTestService;

    /// <summary>
    /// Access to the network monitor for the main dashboard
    /// </summary>
    public NetworkSpeedMonitor NetworkMonitor => _networkMonitor;

    /// <summary>
    /// Updates the speed display
    /// </summary>
    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        var (download, upload) = _networkMonitor.GetCurrentSpeed();

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

        var showOverlay = new ToolStripMenuItem("Show Overlay");
        showOverlay.Click += (s, e) =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        };

        var openDashboard = new ToolStripMenuItem("Open Dashboard");
        openDashboard.Click += (s, e) => App.ShowMainWindow();

        var separator = new ToolStripSeparator();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitApplication();

        contextMenu.Items.Add(showOverlay);
        contextMenu.Items.Add(openDashboard);
        contextMenu.Items.Add(separator);
        contextMenu.Items.Add(exitItem);
        icon.ContextMenuStrip = contextMenu;

        // Double-click to open dashboard
        icon.DoubleClick += (s, e) => App.ShowMainWindow();

        return icon;
    }

    /// <summary>
    /// Loads the tray icon
    /// </summary>
    private static Icon LoadTrayIcon()
    {
        try
        {
            // Try to load custom icon
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (System.IO.File.Exists(iconPath))
            {
                return new Icon(iconPath);
            }
        }
        catch { }

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
        CleanupResources();
    }

    /// <summary>
    /// Cleans up all resources
    /// </summary>
    private void CleanupResources()
    {
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
        CleanupResources();
        Application.Current.Shutdown();
    }
}
