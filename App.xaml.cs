using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Throughput.Helpers;
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
    private static UpdateWindow? _updateWindow;
    private static NotifyIcon? _trayIcon;
    private static ToolStripMenuItem? _updateMenuItem;
    private static Icon? _badgeIcon;
    private static bool _balloonHandlerAttached;
    private static AppSettings _settings = new();

    // Shared services - accessible from the widget and the dashboard
    private static NetworkSpeedMonitor? _networkMonitor;
    private static SpeedTestService? _speedTestService;
    private static SystemMonitor? _systemMonitor;
    private static UpdateService? _updateService;

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

    /// <summary>Shared GitHub update checker/downloader (lazily created).</summary>
    public static UpdateService Updater => _updateService ??= new UpdateService();

    /// <summary>The newer release found on launch, if any.</summary>
    public static UpdateInfo? AvailableUpdate { get; private set; }

    /// <summary>Current application settings.</summary>
    public static AppSettings Settings => _settings;

    /// <summary>True if the widget is currently shown.</summary>
    public static bool IsWidgetVisible => _widget != null && _widget.IsVisible;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Handle unhandled exceptions. The full exception (including inner
        // exceptions and stack) goes to a log file - a bare Message is rarely
        // enough to diagnose failures that only reproduce in Release builds.
        DispatcherUnhandledException += (s, args) =>
        {
            string log = LogException(args.Exception);

            System.Windows.MessageBox.Show(
                $"An error occurred: {args.Exception.Message}\n\nDetails written to:\n{log}",
                "Throughput Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        // Load settings
        _settings = AppSettings.Load();
        WarningGlow.AnimationEnabled = _settings.Animation.Enabled;
        WarningGlow.WarningThreshold = _settings.Animation.WarningThreshold;
        WarningGlow.CriticalThreshold = _settings.Animation.CriticalThreshold;

        // System tray + auto-show the widget on startup
        _trayIcon = CreateTrayIcon();
        ShowWidget();

        // Build the always-on monitors off the startup path - enumerating network
        // adapters takes long enough to visibly delay the widget's first paint at
        // boot. Both readers null-check these, so the first tick just reads 0.
        // SpeedTestService stays lazy; only the dashboard's speed test needs it.
        _ = Task.Run(() =>
        {
            _networkMonitor = new NetworkSpeedMonitor();
            _systemMonitor = new SystemMonitor();
        });

        // Check GitHub for a newer release in the background (non-blocking).
        _ = CheckForUpdatesOnStartupAsync();

        // Compact the LOH and release startup peak allocations now that the
        // app has settled into its quiet steady state, then hand the freed
        // pages back to the OS once the startup work drains.
        GC.Collect(2, GCCollectionMode.Optimized, blocking: false, compacting: true);
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            () => MemoryTrimmer.Trim());
    }

    /// <summary>
    /// On-launch update check. Marks the tray + pops the update window when a newer
    /// release is available and hasn't been skipped. Fails silently (e.g. offline).
    /// </summary>
    private static async Task CheckForUpdatesOnStartupAsync()
    {
        if (!_settings.Update.AutoCheckEnabled) return;

        try
        {
            var info = await Updater.CheckForUpdateAsync();
            if (info == null) return;

            Current.Dispatcher.Invoke(() =>
            {
                OnUpdateFound(info);

                string versionKey = $"{info.Version.Major}.{info.Version.Minor}.{info.Version.Build}";
                if (_settings.Update.SkippedVersion != versionKey)
                {
                    ShowUpdateWindow();
                }
            });
        }
        catch
        {
            // Offline / API error / parse failure: silently skip the auto-check.
        }
    }

    /// <summary>
    /// Appends a full exception dump to %APPDATA%\Throughput\error.log and returns
    /// the log path. Best-effort: never throws from the crash handler itself.
    /// </summary>
    private static string LogException(Exception ex)
    {
        string path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Throughput", "error.log");

        try
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);

            System.IO.File.AppendAllText(path,
                $"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss}  v{AppInfo.Current} =====\n{ex}\n\n");
        }
        catch { }

        return path;
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

        // Hidden until an update is found on startup.
        _updateMenuItem = new ToolStripMenuItem("Update available") { Visible = false };
        _updateMenuItem.Click += (s, e) => ShowUpdateWindow();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitApplication();

        contextMenu.Items.Add(showWidget);
        contextMenu.Items.Add(hideWidget);
        contextMenu.Items.Add(openDashboard);
        contextMenu.Items.Add(_updateMenuItem);
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
    /// Records a discovered update and badges the tray. Called from the startup
    /// check and from a manual dashboard check.
    /// </summary>
    public static void OnUpdateFound(UpdateInfo info)
    {
        AvailableUpdate = info;
        SetTrayUpdateAvailable(info);
    }

    /// <summary>
    /// Raises a tray notification once an update finishes downloading, so a user who
    /// minimized the updater and went back to work knows it's ready to install.
    /// Clicking the notification brings the updater back up.
    /// </summary>
    public static void NotifyUpdateReady(UpdateInfo info)
    {
        if (_trayIcon == null) return;

        if (!_balloonHandlerAttached)
        {
            _trayIcon.BalloonTipClicked += (_, _) => ShowUpdateWindow();
            _balloonHandlerAttached = true;
        }

        string versionLabel = $"v{info.Version.Major}.{info.Version.Minor}.{info.Version.Build}";
        _trayIcon.ShowBalloonTip(
            10000,
            $"Throughput {versionLabel} is ready",
            "The update has been downloaded. Click here to install it.",
            ToolTipIcon.Info);
    }

    /// <summary>
    /// Shows the update popup for the available update (creates it on first call).
    /// </summary>
    public static void ShowUpdateWindow()
    {
        if (AvailableUpdate == null) return;

        if (_updateWindow == null)
        {
            _updateWindow = new UpdateWindow(AvailableUpdate);
            _updateWindow.Closed += (_, _) => _updateWindow = null;
        }

        _updateWindow.Show();
        _updateWindow.Activate();
        if (_updateWindow.WindowState == WindowState.Minimized)
        {
            _updateWindow.WindowState = WindowState.Normal;
        }
    }

    /// <summary>
    /// Badges the tray icon with a green dot + tooltip and reveals the tray update
    /// menu item to signal that an update is available.
    /// </summary>
    private static void SetTrayUpdateAvailable(UpdateInfo info)
    {
        if (_trayIcon == null) return;

        string versionLabel = $"v{info.Version.Major}.{info.Version.Minor}.{info.Version.Build}";

        try
        {
            var badge = CreateBadgedTrayIcon();
            if (badge != null)
            {
                _trayIcon.Icon = badge;
                _badgeIcon?.Dispose();
                _badgeIcon = badge;
            }
        }
        catch { /* keep the plain icon if compositing fails */ }

        _trayIcon.Text = $"Throughput — Update available ({versionLabel})";

        if (_updateMenuItem != null)
        {
            _updateMenuItem.Text = $"Update to {versionLabel}…";
            _updateMenuItem.Visible = true;
        }
    }

    /// <summary>
    /// Draws the app icon with a small green dot in the lower-right corner.
    /// </summary>
    private static Icon? CreateBadgedTrayIcon()
    {
        using var baseIcon = LoadTrayIcon();
        using var bmp = baseIcon.ToBitmap();
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float d = bmp.Width * 0.42f;
            float x = bmp.Width - d;
            float y = bmp.Height - d;

            using var green = new SolidBrush(Color.FromArgb(0x3D, 0xDC, 0xA4));
            using var ring = new Pen(Color.FromArgb(0x0B, 0x0E, 0x16), Math.Max(1f, d * 0.16f));
            g.FillEllipse(green, x, y, d - 1, d - 1);
            g.DrawEllipse(ring, x, y, d - 1, d - 1);
        }

        IntPtr hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
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
        _updateService?.Dispose();
        _badgeIcon?.Dispose();

        Current.Shutdown();
    }
}
