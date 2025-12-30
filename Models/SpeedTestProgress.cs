namespace Throughput.Models;

/// <summary>
/// Current phase of the speed test
/// </summary>
public enum SpeedTestPhase
{
    /// <summary>
    /// Not currently running a test
    /// </summary>
    Idle,

    /// <summary>
    /// Measuring network latency
    /// </summary>
    Latency,

    /// <summary>
    /// Measuring download speed
    /// </summary>
    Download,

    /// <summary>
    /// Measuring upload speed
    /// </summary>
    Upload,

    /// <summary>
    /// Test completed
    /// </summary>
    Complete
}

/// <summary>
/// Progress update during speed test
/// </summary>
public class SpeedTestProgress
{
    /// <summary>
    /// Current phase of the test
    /// </summary>
    public SpeedTestPhase Phase { get; set; }

    /// <summary>
    /// Current measured speed in Mbps (for download/upload phases)
    /// </summary>
    public double CurrentSpeedMbps { get; set; }

    /// <summary>
    /// Current latency in ms (for latency phase)
    /// </summary>
    public double CurrentLatencyMs { get; set; }

    /// <summary>
    /// Overall progress percentage (0-100)
    /// </summary>
    public double ProgressPercent { get; set; }

    /// <summary>
    /// Human-readable status message
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;
}
