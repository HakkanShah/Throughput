using System.Diagnostics;
using System.Net.NetworkInformation;

namespace Throughput.Services;

/// <summary>
/// Monitors network throughput by sampling the active adapters' cumulative byte
/// counters (<see cref="NetworkInterface.GetIPStatistics"/>) on its own 1-second
/// timer and caching the latest rate. Every consumer (widget + dashboard) reads
/// the same cached value via <see cref="GetCurrentSpeed"/>, so their readouts stay
/// consistent regardless of how or when each one polls. Summing across adapters
/// avoids missing traffic, and the interface counters keep this free of
/// <see cref="System.Diagnostics.PerformanceCounter"/>'s heavy PDH machinery.
/// </summary>
public sealed class NetworkSpeedMonitor : IDisposable
{
    private readonly System.Threading.Timer _sampleTimer;
    private readonly object _lock = new();

    private List<NetworkInterface> _adapters = new();
    private string _adapterSignature = string.Empty;
    private DateTime _lastAdapterRefresh = DateTime.MinValue;
    private static readonly TimeSpan AdapterRefreshInterval = TimeSpan.FromSeconds(5);

    private long _lastBytesReceived;
    private long _lastBytesSent;
    private long _lastTimestamp;
    private bool _hasSample;

    private double _cachedDownload;
    private double _cachedUpload;
    private bool _disposed;

    public NetworkSpeedMonitor()
    {
        RefreshAdapters();
        Sample(); // prime the baseline
        _sampleTimer = new System.Threading.Timer(_ => SafeSample(), null,
            dueTime: 1000, period: 1000);
    }

    /// <summary>
    /// Gets the most recent download/upload speed in bytes per second. Returns the
    /// cached value from the monitor's own 1-second sampling loop.
    /// </summary>
    public (double Download, double Upload) GetCurrentSpeed()
    {
        lock (_lock)
        {
            return (_cachedDownload, _cachedUpload);
        }
    }

    private void SafeSample()
    {
        // Timer callbacks run on the thread pool; an unhandled exception here would
        // crash the process, so never let one escape.
        try { Sample(); }
        catch (Exception ex) { Debug.WriteLine($"Network sample failed: {ex.Message}"); }
    }

    /// <summary>
    /// Reads the adapters, computes the rate since the previous sample, and caches it.
    /// </summary>
    private void Sample()
    {
        if (DateTime.Now - _lastAdapterRefresh > AdapterRefreshInterval)
        {
            _lastAdapterRefresh = DateTime.Now;
            if (RefreshAdapters())
            {
                _hasSample = false; // adapter set changed; baseline is stale
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
                _lastAdapterRefresh = DateTime.MinValue; // force a rescan next tick
            }
        }

        long now = Stopwatch.GetTimestamp();

        if (!_hasSample)
        {
            _lastBytesReceived = received;
            _lastBytesSent = sent;
            _lastTimestamp = now;
            _hasSample = true;
            SetCached(0, 0);
            return;
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
            SetCached(0, 0);
            return;
        }

        SetCached(recvDelta / seconds, sentDelta / seconds);
    }

    private void SetCached(double download, double upload)
    {
        lock (_lock)
        {
            _cachedDownload = download;
            _cachedUpload = upload;
        }
    }

    /// <summary>
    /// Rebuilds the list of active physical adapters. Returns true if the set changed.
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
        _sampleTimer.Dispose();
    }
}
