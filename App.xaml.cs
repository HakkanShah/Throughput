using System.Windows;
using WpfApplication = System.Windows.Application;

namespace Throughput;

/// <summary>
/// Application entry point
/// </summary>
public partial class App : WpfApplication
{
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
    }
}
