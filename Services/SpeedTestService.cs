using System.Diagnostics;
using System.Net.Http;

namespace Throughput.Services;

/// <summary>
/// Performs bandwidth speed tests by downloading from public CDNs
/// </summary>
public sealed class SpeedTestService : IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _isRunning;
    private bool _disposed;

    // Test files from reliable CDNs (small files for quick tests)
    private static readonly string[] TestUrls =
    [
        "https://speed.cloudflare.com/__down?bytes=10000000",    // Cloudflare 10MB
        "https://proof.ovh.net/files/10Mb.dat",                   // OVH 10MB
        "http://speedtest.tele2.net/10MB.zip"                     // Tele2 10MB
    ];

    public SpeedTestService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Whether a speed test is currently running
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Event raised when speed test progress updates
    /// </summary>
    public event Action<double>? ProgressChanged;

    /// <summary>
    /// Event raised when speed test completes
    /// </summary>
    public event Action<SpeedTestResult>? TestCompleted;

    /// <summary>
    /// Runs a download speed test
    /// </summary>
    public async Task RunTestAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning) return;
        
        _isRunning = true;
        var result = new SpeedTestResult();

        try
        {
            // Try each URL until one works
            foreach (var url in TestUrls)
            {
                try
                {
                    var speed = await MeasureDownloadSpeedAsync(url, cancellationToken);
                    if (speed > 0)
                    {
                        result.DownloadSpeedMbps = speed;
                        result.Success = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Speed test failed for {url}: {ex.Message}");
                    // Try next URL
                }
            }

            if (!result.Success)
            {
                result.ErrorMessage = "Could not connect to speed test servers";
            }
        }
        catch (OperationCanceledException)
        {
            result.ErrorMessage = "Speed test cancelled";
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            _isRunning = false;
            TestCompleted?.Invoke(result);
        }
    }

    /// <summary>
    /// Measures download speed from a URL
    /// </summary>
    private async Task<double> MeasureDownloadSpeedAsync(string url, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        long totalBytes = 0;
        var buffer = new byte[81920]; // 80KB buffer

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        int bytesRead;
        var lastProgressUpdate = stopwatch.ElapsedMilliseconds;

        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            totalBytes += bytesRead;

            // Update progress every 200ms
            if (stopwatch.ElapsedMilliseconds - lastProgressUpdate > 200)
            {
                var currentSpeedMbps = (totalBytes * 8.0) / (stopwatch.Elapsed.TotalSeconds * 1_000_000);
                ProgressChanged?.Invoke(currentSpeedMbps);
                lastProgressUpdate = stopwatch.ElapsedMilliseconds;
            }
        }

        stopwatch.Stop();

        // Calculate speed in Mbps (megabits per second)
        var speedMbps = (totalBytes * 8.0) / (stopwatch.Elapsed.TotalSeconds * 1_000_000);
        return speedMbps;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Result of a speed test
/// </summary>
public class SpeedTestResult
{
    public bool Success { get; set; }
    public double DownloadSpeedMbps { get; set; }
    public string? ErrorMessage { get; set; }
}
