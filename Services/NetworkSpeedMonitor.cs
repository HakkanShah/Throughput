using System.Diagnostics;
using System.Net.NetworkInformation;

namespace Throughput.Services;

/// <summary>
/// Monitors network throughput by summing the cumulative byte counters of every
/// active physical adapter (<see cref="NetworkInterface.GetIPStatistics"/>) and
/// dividing the delta by the precisely-measured elapsed time. Summing avoids
/// missing traffic when more than one adapter is up, and using the interface
/// counters keeps this free of <see cref="System.Diagnostics.PerformanceCounter"/>'s
/// heavy PDH machinery.
/// </summary>
public sealed class NetworkSpeedMonitor : IDisposable
{
    private List<NetworkInterface> _adapters = new();
    private string _adapterSignature = string.Empty;
    private DateTime _lastAdapterRefresh = DateTime.MinValue;
    private static readonly TimeSpan AdapterRefreshInterval = TimeSpan.FromSeconds(5);

    private long _lastBytesReceived;
    private long _lastBytesSent;
    private long _lastTimestamp;
    private bool _hasSample;
    private bool _disposed;

    public NetworkSpeedMonitor()
    {
        RefreshAdapters();
    }

    /// <summary>
    /// Gets the current download and upload speed in bytes per second, summed over
    /// all active physical adapters.
    /// </summary>
    public (double Download, double Upload) GetCurrentSpeed()
    {
        // Re-scan the adapter set occasionally; only invalidate the running total
        // baseline when the set of adapters actually changes.
        if (DateTime.Now - _lastAdapterRefresh > AdapterRefreshInterval)
        {
            _lastAdapterRefresh = DateTime.Now;
            if (RefreshAdapters())
            {
                _hasSample = false;
            }
        }

        long received = 0;
        long sent = 0;
        foreach (var adapter in _adapters)
        {
            try
            {
                var stats = adapter.GetIPStatistics();
                received += stats.BytesReceived;
                sent += stats.BytesSent;
            }
            catch
            {
                // Adapter vanished (unplugged/disabled); force a rescan next tick.
                _lastAdapterRefresh = DateTime.MinValue;
            }
        }

        long now = Stopwatch.GetTimestamp();

        if (!_hasSample)
        {
            _lastBytesReceived = received;
            _lastBytesSent = sent;
            _lastTimestamp = now;
            _hasSample = true;
            return (0, 0);
        }

        double seconds = (now - _lastTimestamp) / (double)Stopwatch.Frequency;
        long recvDelta = received - _lastBytesReceived;
        long sentDelta = sent - _lastBytesSent;

        _lastBytesReceived = received;
        _lastBytesSent = sent;
        _lastTimestamp = now;

        // Guard against counter resets/wrap after sleep or an adapter change.
        if (seconds <= 0 || recvDelta < 0 || sentDelta < 0)
        {
            return (0, 0);
        }

        return (recvDelta / seconds, sentDelta / seconds);
    }

    /// <summary>
    /// Rebuilds the list of active physical adapters. Returns true if the set
    /// changed since the last refresh.
    /// </summary>
    private bool RefreshAdapters()
    {
        var adapters = new List<NetworkInterface>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                if (ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)) continue;
                if (ni.Description.Contains("Loopback", StringComparison.OrdinalIgnoreCase)) continue;
                adapters.Add(ni);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to enumerate network adapters: {ex.Message}");
        }

        string signature = string.Join("|", adapters.Select(a => a.Id).OrderBy(id => id));
        bool changed = signature != _adapterSignature;

        _adapters = adapters;
        _adapterSignature = signature;
        return changed;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
