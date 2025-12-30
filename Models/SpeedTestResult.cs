namespace Throughput.Models;

/// <summary>
/// Result of a complete speed test
/// </summary>
public class SpeedTestResult
{
    /// <summary>
    /// Whether the test completed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Download speed in megabits per second
    /// </summary>
    public double DownloadSpeedMbps { get; set; }

    /// <summary>
    /// Upload speed in megabits per second
    /// </summary>
    public double UploadSpeedMbps { get; set; }

    /// <summary>
    /// Network latency (round-trip time) in milliseconds
    /// </summary>
    public double LatencyMs { get; set; }

    /// <summary>
    /// Latency variation (jitter) in milliseconds
    /// </summary>
    public double JitterMs { get; set; }

    /// <summary>
    /// Information about the test server used
    /// </summary>
    public string? ServerInfo { get; set; }

    /// <summary>
    /// Error message if test failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the test was performed
    /// </summary>
    public DateTime TestTimestamp { get; set; } = DateTime.Now;
}
