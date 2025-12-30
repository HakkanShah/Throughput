using System.Diagnostics;
using System.Net.NetworkInformation;

namespace Throughput.Services;

/// <summary>
/// Monitors network speed using Windows Performance Counters
/// </summary>
public sealed class NetworkSpeedMonitor : IDisposable
{
    private PerformanceCounter? _bytesReceivedCounter;
    private PerformanceCounter? _bytesSentCounter;
    private string? _activeAdapterName;
    private bool _disposed;
    private DateTime _lastAdapterCheck = DateTime.MinValue;
    private static readonly TimeSpan AdapterCheckInterval = TimeSpan.FromSeconds(30);

    public NetworkSpeedMonitor()
    {
        InitializeCounters();
    }

    /// <summary>
    /// Initializes performance counters for the active network adapter
    /// </summary>
    private void InitializeCounters()
    {
        try
        {
            var adapterName = GetActiveNetworkAdapter();
            if (string.IsNullOrEmpty(adapterName))
            {
                return;
            }

            _activeAdapterName = adapterName;
            
            _bytesReceivedCounter = new PerformanceCounter(
                "Network Interface",
                "Bytes Received/sec",
                adapterName,
                true);

            _bytesSentCounter = new PerformanceCounter(
                "Network Interface",
                "Bytes Sent/sec",
                adapterName,
                true);

            // Initial read to prime the counters
            _bytesReceivedCounter.NextValue();
            _bytesSentCounter.NextValue();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize network counters: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current download and upload speed in bytes per second
    /// </summary>
    public (double Download, double Upload) GetCurrentSpeed()
    {
        // Periodically check if we need to switch adapters
        if (DateTime.Now - _lastAdapterCheck > AdapterCheckInterval)
        {
            _lastAdapterCheck = DateTime.Now;
            CheckAndSwitchAdapter();
        }

        try
        {
            double download = _bytesReceivedCounter?.NextValue() ?? 0;
            double upload = _bytesSentCounter?.NextValue() ?? 0;
            return (download, upload);
        }
        catch
        {
            // Counter may have become invalid, try to reinitialize
            ReinitializeCounters();
            return (0, 0);
        }
    }

    /// <summary>
    /// Checks if the active adapter has changed and switches if needed
    /// </summary>
    private void CheckAndSwitchAdapter()
    {
        var currentAdapter = GetActiveNetworkAdapter();
        if (currentAdapter != _activeAdapterName && !string.IsNullOrEmpty(currentAdapter))
        {
            ReinitializeCounters();
        }
    }

    /// <summary>
    /// Reinitializes counters (used when adapter changes)
    /// </summary>
    private void ReinitializeCounters()
    {
        DisposeCounters();
        InitializeCounters();
    }

    /// <summary>
    /// Gets the name of the active network adapter as it appears in Performance Counters
    /// </summary>
    private static string? GetActiveNetworkAdapter()
    {
        try
        {
            // Get all network interfaces that are up and not loopback
            var activeInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .Where(ni => !ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                .Where(ni => !ni.Description.Contains("Loopback", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (activeInterfaces.Count == 0)
            {
                return null;
            }

            // Get performance counter instance names
            var category = new PerformanceCounterCategory("Network Interface");
            var instanceNames = category.GetInstanceNames();

            // Find a matching instance name for an active interface
            foreach (var ni in activeInterfaces)
            {
                // Performance counter names replace special characters
                var possibleNames = new[]
                {
                    ni.Description,
                    ni.Name,
                    SanitizeInstanceName(ni.Description)
                };

                foreach (var instanceName in instanceNames)
                {
                    foreach (var possibleName in possibleNames)
                    {
                        if (instanceName.Contains(possibleName, StringComparison.OrdinalIgnoreCase) ||
                            possibleName.Contains(instanceName, StringComparison.OrdinalIgnoreCase) ||
                            SanitizeInstanceName(instanceName) == SanitizeInstanceName(possibleName))
                        {
                            return instanceName;
                        }
                    }
                }
            }

            // Fallback: return the first non-loopback instance
            return instanceNames.FirstOrDefault(n => 
                !n.Contains("Loopback", StringComparison.OrdinalIgnoreCase) &&
                !n.Contains("isatap", StringComparison.OrdinalIgnoreCase) &&
                !n.Contains("Teredo", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get active network adapter: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sanitizes instance name to match performance counter naming
    /// </summary>
    private static string SanitizeInstanceName(string name)
    {
        // Performance counters replace certain characters
        return name
            .Replace('(', '[')
            .Replace(')', ']')
            .Replace('#', '_')
            .Replace('/', '_')
            .Replace('\\', '_');
    }

    private void DisposeCounters()
    {
        _bytesReceivedCounter?.Dispose();
        _bytesReceivedCounter = null;
        
        _bytesSentCounter?.Dispose();
        _bytesSentCounter = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        DisposeCounters();
        _disposed = true;
    }
}
