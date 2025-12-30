namespace Throughput.Helpers;

/// <summary>
/// Utility class for formatting speed values
/// </summary>
public static class SpeedFormatter
{
    private static readonly string[] ByteUnits = ["B/s", "KB/s", "MB/s", "GB/s"];
    private static readonly string[] BitUnits = ["bps", "Kbps", "Mbps", "Gbps"];

    /// <summary>
    /// Formats bytes per second to human readable format (B/s, KB/s, MB/s, GB/s)
    /// </summary>
    public static string FormatBytesPerSecond(double bytesPerSecond)
    {
        int unitIndex = 0;
        double speed = bytesPerSecond;

        while (speed >= 1024 && unitIndex < ByteUnits.Length - 1)
        {
            speed /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{speed:F0} {ByteUnits[unitIndex]}"
            : $"{speed:F1} {ByteUnits[unitIndex]}";
    }

    /// <summary>
    /// Formats megabits per second with appropriate unit
    /// </summary>
    public static string FormatMbps(double mbps)
    {
        if (mbps >= 1000)
        {
            return $"{mbps / 1000:F2} Gbps";
        }
        if (mbps >= 100)
        {
            return $"{mbps:F0} Mbps";
        }
        if (mbps >= 10)
        {
            return $"{mbps:F1} Mbps";
        }
        return $"{mbps:F2} Mbps";
    }

    /// <summary>
    /// Formats latency in milliseconds
    /// </summary>
    public static string FormatLatency(double ms)
    {
        if (ms < 1)
        {
            return $"{ms * 1000:F0} µs";
        }
        if (ms < 10)
        {
            return $"{ms:F1} ms";
        }
        return $"{ms:F0} ms";
    }

    /// <summary>
    /// Converts bytes per second to megabits per second
    /// </summary>
    public static double BytesToMbps(double bytesPerSecond)
    {
        return (bytesPerSecond * 8) / 1_000_000;
    }

    /// <summary>
    /// Converts megabits per second to bytes per second
    /// </summary>
    public static double MbpsToBytes(double mbps)
    {
        return (mbps * 1_000_000) / 8;
    }
}
