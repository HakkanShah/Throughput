using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Throughput.Models;
using Throughput.Services;
using Throughput.Windows;
using WpfApplication = System.Windows.Application;

namespace Throughput;

/// <summary>
/// Application entry point - hosts the single Widget, the dashboard, and the
/// shared monitoring services.
/// </summary>
public partial class App : WpfApplication
{
    private static Widget? _widget;
    private static MainAppWindow? _mainAppWindow;
    private static NotifyIcon? _trayIcon;
    private static AppSettings _settings = new();

    // Shared services - accessible from the widget and the dashboard
    private static NetworkSpeedMonitor? _networkMonitor;
    private static SpeedTestService? _speedTestService;
    private static SystemMonitor? _systemMonitor;

    /// <summary>The current widget instance, if shown.</summary>
    public static Widget? CurrentWidget => _widget;

    /// <summary>The main dashboard window (created on demand).</summary>
    public static MainAppWindow? MainAppWindow => _mainAppWindow;

    /// <summary>Shared network speed monitor.</summary>
    public static NetworkSpeedMonitor? NetworkMonitor => _networkMonitor;

    /// <summary>
    /// Shared on-demand speed test service. Lazily created on first access so
    /// the HttpClient + buffers aren't allocated unless the user actually
    /// triggers a test from the dashboard.
    /// </summary>
    public static SpeedTestService SpeedTestService
        => _speedTestService ??= new SpeedTestService();

    /// <summary>Shared CPU and memory monitor.</summary>
    public static SystemMonitor? SystemMonitor => _systemMonitor;

    /// <summary>Current application settings.</summary>
    public static AppSettings Settings => _settings;

    /// <summary>True if the widget is currently shown.</summary>
    public static bool IsWidgetVisible => _widget != null && _widget.IsVisible;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Handle unhandled exceptions
        DispatcherUnhandledException += (s, args) =>
        {
            System.Windows.MessageBox.Show($"An error occurred: {args.Exception.Message}",
                "Throughput Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        // Load settings
        _settings = AppSettings.Load();

        // Initialize the always-on services. SpeedTestService is created lazily
        // on first dashboard speed-test - the widget itself never needs it.
        _networkMonitor = new NetworkSpeedMonitor();
        _systemMonitor = new SystemMonitor();

        // System tray + auto-show the widget on startup
        _trayIcon = CreateTrayIcon();
        ShowWidget();

        // Compact the LOH and release startup peak allocations now that the
        // app has settled into its quiet steady state.
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false, compacting: true);
    }

    /// <summary>
    /// Creates the system tray icon with its context menu.
    /// </summary>
    private static NotifyIcon CreateTrayIcon()
    {
        var icon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "Throughput - Network Speed Monitor"
        };

        var contextMenu = new ContextMenuStrip();

        var showWidget = new ToolStripMenuItem("Show Widget");
        showWidget.Click += (s, e) => ShowWidget();

        var hideWidget = new ToolStripMenuItem("Hide Widget");
        hideWidget.Click += (s, e) => HideWidget();

        var openDashboard = new ToolStripMenuItem("Open Dashboard");
        openDashboard.Click += (s, e) => ShowMainWindow();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitApplication();

        contextMenu.Items.Add(showWidget);
        contextMenu.Items.Add(hideWidget);
        contextMenu.Items.Add(openDashboard);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);
        icon.ContextMenuStrip = contextMenu;

        // Double-click tray icon opens the dashboard
        icon.DoubleClick += (s, e) => ShowMainWindow();

        return icon;
    }

    /// <summary>
    /// Loads the system tray icon from the Assets folder, falling back to a default.
    /// </summary>
    private static Icon LoadTrayIcon()
    {
        try
        {
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
    /// Shows the widget. Creates it on first call; re-shows it on subsequent calls.
    /// </summary>
    public static void ShowWidget()
    {
        if (_widget == null)
        {
            _widget = new Widget();
        }

        _widget.Show();
        if (_widget.WindowState == WindowState.Minimized)
        {
            _widget.WindowState = WindowState.Normal;
        }
        _widget.Activate();
    }

    /// <summary>
    /// Hides the widget without exiting the application. Tray icon stays available.
    /// </summary>
    public static void HideWidget()
    {
        _widget?.Hide();
    }

    /// <summary>
    /// Shows the main dashboard window (creates it on first call).
    /// </summary>
    public static void ShowMainWindow()
    {
        if (_mainAppWindow == null)
        {
            _mainAppWindow = new MainAppWindow();
        }

        _mainAppWindow.Show();
        _mainAppWindow.Activate();

        if (_mainAppWindow.WindowState == WindowState.Minimized)
        {
            _mainAppWindow.WindowState = WindowState.Normal;
        }
    }

    /// <summary>
    /// Hides the main dashboard window.
    /// </summary>
    public static void HideMainWindow()
    {
        _mainAppWindow?.Hide();
    }

    /// <summary>
    /// Exits the application and cleans up resources.
    /// </summary>
    public static void ExitApplication()
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        _networkMonitor?.Dispose();
        _speedTestService?.Dispose();
        _systemMonitor?.Dispose();

        Current.Shutdown();
    }
}
