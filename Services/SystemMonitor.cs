using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Throughput.Services;

/// <summary>
/// Monitors real-time CPU and memory (RAM) usage using Windows Performance Counters.
/// </summary>
public sealed class SystemMonitor : IDisposable
{
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _availableMemoryCounter;
    private readonly double _totalMemoryMb;
    private bool _disposed;

    public SystemMonitor()
    {
        _totalMemoryMb = GetTotalPhysicalMemoryMb();
        InitializeCounters();
    }

    /// <summary>
    /// Total physical RAM in gigabytes.
    /// </summary>
    public double TotalMemoryGb => _totalMemoryMb / 1024.0;

    /// <summary>
    /// Initializes the CPU and memory performance counters.
    /// </summary>
    private void InitializeCounters()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
            _availableMemoryCounter = new PerformanceCounter("Memory", "Available MBytes", true);

            // Prime the CPU counter (first read is always 0).
            _cpuCounter.NextValue();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize system counters: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current CPU and memory usage.
    /// </summary>
    /// <returns>
    /// CpuPercent (0-100), MemoryPercent (0-100),
    /// MemoryUsedGb and MemoryTotalGb.
    /// </returns>
    public (double CpuPercent, double MemoryPercent, double MemoryUsedGb, double MemoryTotalGb) GetUsage()
    {
        try
        {
            double cpu = _cpuCounter?.NextValue() ?? 0;
            cpu = Math.Clamp(cpu, 0, 100);

            double availableMb = _availableMemoryCounter?.NextValue() ?? 0;
            double usedMb = Math.Max(0, _totalMemoryMb - availableMb);
            double memoryPercent = _totalMemoryMb > 0
                ? Math.Clamp(usedMb / _totalMemoryMb * 100, 0, 100)
                : 0;

            return (cpu, memoryPercent, usedMb / 1024.0, _totalMemoryMb / 1024.0);
        }
        catch
        {
            ReinitializeCounters();
            return (0, 0, 0, _totalMemoryMb / 1024.0);
        }
    }

    /// <summary>
    /// Reinitializes counters after a failure.
    /// </summary>
    private void ReinitializeCounters()
    {
        DisposeCounters();
        InitializeCounters();
    }

    /// <summary>
    /// Gets total installed physical memory in megabytes via the Win32 API.
    /// </summary>
    private static double GetTotalPhysicalMemoryMb()
    {
        try
        {
            var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (GlobalMemoryStatusEx(ref status))
            {
                return status.ullTotalPhys / (1024.0 * 1024.0);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to read total physical memory: {ex.Message}");
        }
        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    private void DisposeCounters()
    {
        _cpuCounter?.Dispose();
        _cpuCounter = null;

        _availableMemoryCounter?.Dispose();
        _availableMemoryCounter = null;
    }

    public void Dispose()
    {
        if (_disposed) return;

        DisposeCounters();
        _disposed = true;
    }
}
