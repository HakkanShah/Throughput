using System.Windows;
using Throughput.Windows;
using WpfApplication = System.Windows.Application;

namespace Throughput;

/// <summary>
/// Application entry point with dual-window management
/// </summary>
public partial class App : WpfApplication
{
    private static OverlayWindow? _overlayWindow;
    private static MainAppWindow? _mainAppWindow;

    /// <summary>
    /// The overlay window instance (always running)
    /// </summary>
    public static OverlayWindow? OverlayWindow => _overlayWindow;

    /// <summary>
    /// The main app window instance (created on demand)
    /// </summary>
    public static MainAppWindow? MainAppWindow => _mainAppWindow;

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

        // Create and show the overlay window
        _overlayWindow = new OverlayWindow();
        _overlayWindow.Show();
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
}
