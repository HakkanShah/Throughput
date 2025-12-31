using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Throughput.Models;
using Throughput.Services;
using Throughput.Windows;
using WpfApplication = System.Windows.Application;

namespace Throughput;

/// <summary>
/// Application entry point with multi-widget management
/// </summary>
public partial class App : WpfApplication
{
    private static Window? _currentWidget;
    private static MainAppWindow? _mainAppWindow;
    private static NotifyIcon? _trayIcon;
    private static AppSettings _settings = new();

    // Shared services - accessible from all widgets
    private static NetworkSpeedMonitor? _networkMonitor;
    private static SpeedTestService? _speedTestService;

    /// <summary>
    /// The current widget window instance
    /// </summary>
    public static Window? CurrentWidget => _currentWidget;

    /// <summary>
    /// The main app window instance (created on demand)
    /// </summary>
    public static MainAppWindow? MainAppWindow => _mainAppWindow;

    /// <summary>
    /// Shared network speed monitor
    /// </summary>
    public static NetworkSpeedMonitor? NetworkMonitor => _networkMonitor;

    /// <summary>
    /// Shared speed test service
    /// </summary>
    public static SpeedTestService? SpeedTestService => _speedTestService;

    /// <summary>
    /// Current application settings
    /// </summary>
    public static AppSettings Settings => _settings;

    /// <summary>
    /// Current widget type
    /// </summary>
    public static WidgetType CurrentWidgetType { get; private set; }

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

        // Initialize shared services
        _networkMonitor = new NetworkSpeedMonitor();
        _speedTestService = new SpeedTestService();

        // Create system tray icon
        _trayIcon = CreateTrayIcon();

        // Create and show the widget based on settings
        SwitchWidget(_settings.DefaultWidgetType);
    }

    /// <summary>
    /// Creates the system tray icon with context menu
    /// </summary>
    private static NotifyIcon CreateTrayIcon()
    {
        var icon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "Throughput - Network Speed Monitor"
        };

        // Create context menu
        var contextMenu = new ContextMenuStrip();

        var showWidget = new ToolStripMenuItem("Show Widget");
        showWidget.Click += (s, e) =>
        {
            _currentWidget?.Show();
            if (_currentWidget != null)
            {
                _currentWidget.WindowState = WindowState.Normal;
                _currentWidget.Activate();
            }
        };

        var openDashboard = new ToolStripMenuItem("Open Dashboard");
        openDashboard.Click += (s, e) => ShowMainWindow();

        // Widget selection submenu
        var widgetMenu = new ToolStripMenuItem("Widget Style");
        foreach (WidgetType widgetType in Enum.GetValues<WidgetType>())
        {
            var item = new ToolStripMenuItem(GetWidgetDisplayName(widgetType));
            item.Tag = widgetType;
            item.Click += (s, e) =>
            {
                if (s is ToolStripMenuItem menuItem && menuItem.Tag is WidgetType type)
                {
                    SwitchWidget(type);
                }
            };
            widgetMenu.DropDownItems.Add(item);
        }

        var separator = new ToolStripSeparator();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitApplication();

        contextMenu.Items.Add(showWidget);
        contextMenu.Items.Add(openDashboard);
        contextMenu.Items.Add(widgetMenu);
        contextMenu.Items.Add(separator);
        contextMenu.Items.Add(exitItem);
        icon.ContextMenuStrip = contextMenu;

        // Double-click to open dashboard
        icon.DoubleClick += (s, e) => ShowMainWindow();

        return icon;
    }

    /// <summary>
    /// Gets display name for widget type
    /// </summary>
    private static string GetWidgetDisplayName(WidgetType type) => type switch
    {
        WidgetType.Full => "Full (Default)",
        WidgetType.Compact => "Compact (Speed Only)",
        WidgetType.Minimal => "Minimal (Download Only)",
        WidgetType.SpeedTest => "Speed Test",
        _ => type.ToString()
    };

    /// <summary>
    /// Loads the tray icon
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
    /// Switches to a different widget type
    /// </summary>
    /// <param name="widgetType">The widget type to switch to</param>
    /// <param name="saveAsDefault">Whether to save this as the default widget</param>
    /// <param name="positionBelowWindow">Optional window to position the new widget below</param>
    public static void SwitchWidget(WidgetType widgetType, bool saveAsDefault = false, Window? positionBelowWindow = null)
    {
        // Close current widget
        _currentWidget?.Close();

        // Create new widget
        _currentWidget = widgetType switch
        {
            WidgetType.Full => new OverlayWindow(),
            WidgetType.Compact => new CompactWidget(),
            WidgetType.Minimal => new MinimalWidget(),
            WidgetType.SpeedTest => new SpeedTestWidget(),
            _ => new OverlayWindow()
        };

        CurrentWidgetType = widgetType;
        
        // Position below the specified window if provided
        if (positionBelowWindow != null && positionBelowWindow.IsVisible)
        {
            _currentWidget.Left = positionBelowWindow.Left;
            _currentWidget.Top = positionBelowWindow.Top + positionBelowWindow.ActualHeight + 10;
        }
        
        _currentWidget.Show();

        // Save preference if requested
        if (saveAsDefault)
        {
            _settings.DefaultWidgetType = widgetType;
            _settings.Save();
        }
    }

    /// <summary>
    /// Shows the main dashboard window (creates it if needed)
    /// </summary>
    public static void ShowMainWindow()
    {
        if (_mainAppWindow == null)
        {
            _mainAppWindow = new MainAppWindow();
        }

        _mainAppWindow.Show();
        _mainAppWindow.Activate();

        // Bring to front
        if (_mainAppWindow.WindowState == WindowState.Minimized)
        {
            _mainAppWindow.WindowState = WindowState.Normal;
        }
    }

    /// <summary>
    /// Closes the main dashboard window
    /// </summary>
    public static void HideMainWindow()
    {
        _mainAppWindow?.Hide();
    }

    /// <summary>
    /// Exits the application and cleans up resources
    /// </summary>
    public static void ExitApplication()
    {
        // Cleanup tray icon
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        // Cleanup services
        _networkMonitor?.Dispose();
        _speedTestService?.Dispose();

        Current.Shutdown();
    }
}
