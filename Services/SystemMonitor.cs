using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Throughput.Services;

/// <summary>
/// Monitors real-time CPU and memory (RAM) usage using lightweight Win32 calls
/// (<c>GetSystemTimes</c> + <c>GlobalMemoryStatusEx</c>), sampled on its own
/// 1-second timer and cached so the widget and dashboard always read the same
/// value. This deliberately avoids <see cref="System.Diagnostics.PerformanceCounter"/>,
/// whose PDH machinery adds tens of megabytes of idle memory to the process.
/// </summary>
public sealed class SystemMonitor : IDisposable
{
    private readonly System.Threading.Timer _sampleTimer;
    private readonly object _lock = new();

    private ulong _prevIdle;
    private ulong _prevKernel;
    private ulong _prevUser;
    private bool _hasPrevSample;

    private double _cpu;
    private double _memPercent;
    private double _memUsedGb;
    private double _memTotalGb;
    private bool _disposed;

    public SystemMonitor()
    {
        Sample(); // prime CPU baseline + fill initial memory values
        _sampleTimer = new System.Threading.Timer(_ => SafeSample(), null,
            dueTime: 1000, period: 1000);
    }

    /// <summary>Total physical RAM in gigabytes.</summary>
    public double TotalMemoryGb
    {
        get { lock (_lock) return _memTotalGb; }
    }

    /// <summary>
    /// Gets the most recent CPU and memory usage from the monitor's 1-second sampling
    /// loop: CpuPercent (0-100), MemoryPercent (0-100), MemoryUsedGb and MemoryTotalGb.
    /// </summary>
    public (double CpuPercent, double MemoryPercent, double MemoryUsedGb, double MemoryTotalGb) GetUsage()
    {
        lock (_lock)
        {
            return (_cpu, _memPercent, _memUsedGb, _memTotalGb);
        }
    }

    private void SafeSample()
    {
        try { Sample(); }
        catch (Exception ex) { Debug.WriteLine($"System sample failed: {ex.Message}"); }
    }

    private void Sample()
    {
        double cpu = ReadCpuPercent();
        var (memPercent, usedGb, totalGb) = ReadMemory();

        lock (_lock)
        {
            _cpu = cpu;
            _memPercent = memPercent;
            _memUsedGb = usedGb;
            _memTotalGb = totalGb;
        }
    }

    /// <summary>
    /// Computes CPU load as the busy fraction of the total processor time elapsed
    /// since the previous sample.
    /// </summary>
    private double ReadCpuPercent()
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            return 0;
        }

        ulong idle = ToUInt64(idleTime);
        ulong kernel = ToUInt64(kernelTime);
        ulong user = ToUInt64(userTime);

        if (!_hasPrevSample)
        {
            _prevIdle = idle;
            _prevKernel = kernel;
            _prevUser = user;
            _hasPrevSample = true;
            return 0;
        }

        // Kernel time already includes idle time, so total = kernel + user.
        ulong idleDelta = idle - _prevIdle;
        ulong totalDelta = (kernel - _prevKernel) + (user - _prevUser);

        _prevIdle = idle;
        _prevKernel = kernel;
        _prevUser = user;

        if (totalDelta == 0) return 0;

        double busy = (double)(totalDelta - idleDelta) / totalDelta * 100.0;
        return Math.Clamp(busy, 0, 100);
    }

    /// <summary>
    /// Reads current memory load and totals from the OS.
    /// </summary>
    private static (double Percent, double UsedGb, double TotalGb) ReadMemory()
    {
        try
        {
            var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (GlobalMemoryStatusEx(ref status))
            {
                double totalGb = status.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                double usedGb = (status.ullTotalPhys - status.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0);
                double percent = Math.Clamp(status.dwMemoryLoad, 0, 100);
                return (percent, usedGb, totalGb);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to read memory status: {ex.Message}");
        }
        return (0, 0, 0);
    }

    private static ulong ToUInt64(FileTime ft) => ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public int dwLowDateTime;
        public int dwHighDateTime;
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
    private static extern bool GetSystemTimes(out FileTime lpIdleTime, out FileTime lpKernelTime, out FileTime lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sampleTimer.Dispose();
    }
}
