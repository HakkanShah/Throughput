using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using Throughput.Models;

namespace Throughput.Services;

/// <summary>
/// Performs comprehensive bandwidth speed tests including download, upload, and latency
/// Uses multiple parallel connections for accurate bandwidth estimation
/// </summary>
public sealed class SpeedTestService : IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _isRunning;
    private bool _disposed;

    // Test configuration
    private const int ParallelConnections = 4;
    private const int TestDurationSeconds = 10;
    private const int WarmupDurationMs = 2000; // Exclude first 2 seconds
    private const int LatencyTestCount = 5;
    private const int ProgressUpdateIntervalMs = 200;

    // Cloudflare speed test endpoints (reliable, global CDN)
    private const string DownloadUrl = "https://speed.cloudflare.com/__down?bytes=25000000"; // 25MB
    private const string UploadUrl = "https://speed.cloudflare.com/__up";
    private const string LatencyUrl = "https://speed.cloudflare.com/__down?bytes=0";

    // Fallback endpoints
    private static readonly string[] FallbackDownloadUrls =
    [
        "https://proof.ovh.net/files/10Mb.dat",
        "http://speedtest.tele2.net/10MB.zip"
    ];

    public SpeedTestService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Throughput/2.0");
    }

    /// <summary>
    /// Whether a speed test is currently running
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Event raised when speed test progress updates
    /// </summary>
    public event Action<SpeedTestProgress>? ProgressChanged;

    /// <summary>
    /// Event raised when speed test completes
    /// </summary>
    public event Action<SpeedTestResult>? TestCompleted;

    /// <summary>
    /// Runs a complete speed test (latency, download, upload)
    /// </summary>
    public async Task RunFullTestAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning) return;

        _isRunning = true;
        var result = new SpeedTestResult { TestTimestamp = DateTime.Now };

        try
        {
            // Phase 1: Latency Test
            ReportProgress(SpeedTestPhase.Latency, 0, 0, "Measuring latency...");
            var (latency, jitter) = await MeasureLatencyAsync(cancellationToken);
            result.LatencyMs = latency;
            result.JitterMs = jitter;
            ReportProgress(SpeedTestPhase.Latency, 0, latency, $"Latency: {latency:F0}ms");

            // Phase 2: Download Test
            ReportProgress(SpeedTestPhase.Download, 0, 0, "Testing download speed...");
            result.DownloadSpeedMbps = await MeasureDownloadSpeedAsync(cancellationToken);

            // Phase 3: Upload Test
            ReportProgress(SpeedTestPhase.Upload, 0, 0, "Testing upload speed...");
            result.UploadSpeedMbps = await MeasureUploadSpeedAsync(cancellationToken);

            result.Success = true;
            result.ServerInfo = "Cloudflare CDN";
            ReportProgress(SpeedTestPhase.Complete, 100, 0, "Test complete");
        }
        catch (OperationCanceledException)
        {
            result.ErrorMessage = "Speed test cancelled";
        }
        catch (HttpRequestException ex)
        {
            result.ErrorMessage = $"Network error: {ex.Message}";
            Debug.WriteLine($"Speed test network error: {ex}");
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Test failed: {ex.Message}";
            Debug.WriteLine($"Speed test error: {ex}");
        }
        finally
        {
            _isRunning = false;
            TestCompleted?.Invoke(result);
        }
    }

    /// <summary>
    /// Legacy method for backward compatibility - runs download test only
    /// </summary>
    public async Task RunTestAsync(CancellationToken cancellationToken = default)
    {
        await RunFullTestAsync(cancellationToken);
    }

    /// <summary>
    /// Measures network latency using multiple HTTP HEAD requests
    /// Returns (average latency, jitter) in milliseconds
    /// </summary>
    private async Task<(double Latency, double Jitter)> MeasureLatencyAsync(CancellationToken ct)
    {
        var latencies = new List<double>();
        var stopwatch = new Stopwatch();

        for (int i = 0; i < LatencyTestCount; i++)
        {
            try
            {
                stopwatch.Restart();

                using var request = new HttpRequestMessage(HttpMethod.Head, LatencyUrl);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                stopwatch.Stop();
                latencies.Add(stopwatch.Elapsed.TotalMilliseconds);

                // Small delay between pings
                await Task.Delay(100, ct);
            }
            catch
            {
                // Skip failed pings
            }
        }

        if (latencies.Count == 0)
        {
            return (0, 0);
        }

        double avgLatency = latencies.Average();

        // Calculate jitter as average deviation from mean
        double jitter = latencies.Count > 1
            ? latencies.Select(l => Math.Abs(l - avgLatency)).Average()
            : 0;

        return (avgLatency, jitter);
    }

    /// <summary>
    /// Measures download speed using multiple parallel connections
    /// Excludes initial warm-up period for accuracy
    /// </summary>
    private async Task<double> MeasureDownloadSpeedAsync(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        long totalBytesAfterWarmup = 0;
        long totalBytes = 0;
        var warmupComplete = false;
        var lockObj = new object();
        DateTime warmupEndTime = DateTime.MinValue;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Create parallel download tasks
        var tasks = new List<Task>();
        for (int i = 0; i < ParallelConnections; i++)
        {
            tasks.Add(DownloadWorkerAsync(DownloadUrl, (bytes, isWarmup) =>
            {
                lock (lockObj)
                {
                    totalBytes += bytes;

                    if (!warmupComplete && stopwatch.ElapsedMilliseconds >= WarmupDurationMs)
                    {
                        warmupComplete = true;
                        warmupEndTime = DateTime.Now;
                        totalBytesAfterWarmup = 0;
                    }

                    if (warmupComplete)
                    {
                        totalBytesAfterWarmup += bytes;
                    }

                    // Report progress
                    double elapsed = stopwatch.Elapsed.TotalSeconds;
                    double progress = Math.Min(100, (elapsed / TestDurationSeconds) * 100);
                    double currentSpeed = elapsed > 0 ? (totalBytes * 8.0) / (elapsed * 1_000_000) : 0;

                    ReportProgress(SpeedTestPhase.Download, progress, currentSpeed,
                        $"Download: {currentSpeed:F1} Mbps");
                }
            }, cts.Token));
        }

        // Wait for test duration
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(TestDurationSeconds), ct);
        }
        catch (OperationCanceledException) { }

        cts.Cancel();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch { }

        stopwatch.Stop();

        // Calculate speed excluding warmup
        double measurementDuration = (stopwatch.ElapsedMilliseconds - WarmupDurationMs) / 1000.0;
        if (measurementDuration <= 0) measurementDuration = stopwatch.Elapsed.TotalSeconds;

        double speedMbps = (totalBytesAfterWarmup * 8.0) / (measurementDuration * 1_000_000);

        // Fallback to total if warmup exclusion gives strange results
        if (speedMbps <= 0 || !warmupComplete)
        {
            speedMbps = (totalBytes * 8.0) / (stopwatch.Elapsed.TotalSeconds * 1_000_000);
        }

        return speedMbps;
    }

    /// <summary>
    /// Worker task for downloading data
    /// </summary>
    private async Task DownloadWorkerAsync(string url, Action<long, bool> onBytesReceived, CancellationToken ct)
    {
        var buffer = new byte[81920]; // 80KB buffer
        var warmupStart = DateTime.Now;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(ct);

                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    bool isWarmup = (DateTime.Now - warmupStart).TotalMilliseconds < WarmupDurationMs;
                    onBytesReceived(bytesRead, isWarmup);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Download worker error: {ex.Message}");
                break;
            }
        }
    }

    /// <summary>
    /// Measures upload speed by POSTing random data
    /// </summary>
    private async Task<double> MeasureUploadSpeedAsync(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        long totalBytesAfterWarmup = 0;
        long totalBytes = 0;
        var warmupComplete = false;
        var lockObj = new object();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Create parallel upload tasks
        var tasks = new List<Task>();
        for (int i = 0; i < ParallelConnections; i++)
        {
            tasks.Add(UploadWorkerAsync((bytes) =>
            {
                lock (lockObj)
                {
                    totalBytes += bytes;

                    if (!warmupComplete && stopwatch.ElapsedMilliseconds >= WarmupDurationMs)
                    {
                        warmupComplete = true;
                        totalBytesAfterWarmup = 0;
                    }

                    if (warmupComplete)
                    {
                        totalBytesAfterWarmup += bytes;
                    }

                    // Report progress
                    double elapsed = stopwatch.Elapsed.TotalSeconds;
                    double progress = Math.Min(100, (elapsed / TestDurationSeconds) * 100);
                    double currentSpeed = elapsed > 0 ? (totalBytes * 8.0) / (elapsed * 1_000_000) : 0;

                    ReportProgress(SpeedTestPhase.Upload, progress, currentSpeed,
                        $"Upload: {currentSpeed:F1} Mbps");
                }
            }, cts.Token));
        }

        // Wait for test duration
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(TestDurationSeconds), ct);
        }
        catch (OperationCanceledException) { }

        cts.Cancel();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch { }

        stopwatch.Stop();

        // Calculate speed excluding warmup
        double measurementDuration = (stopwatch.ElapsedMilliseconds - WarmupDurationMs) / 1000.0;
        if (measurementDuration <= 0) measurementDuration = stopwatch.Elapsed.TotalSeconds;

        double speedMbps = (totalBytesAfterWarmup * 8.0) / (measurementDuration * 1_000_000);

        // Fallback
        if (speedMbps <= 0 || !warmupComplete)
        {
            speedMbps = (totalBytes * 8.0) / (stopwatch.Elapsed.TotalSeconds * 1_000_000);
        }

        return speedMbps;
    }

    /// <summary>
    /// Worker task for uploading random data
    /// </summary>
    private async Task UploadWorkerAsync(Action<long> onBytesSent, CancellationToken ct)
    {
        // Generate 1MB of random data to upload repeatedly
        const int chunkSize = 1024 * 1024;
        var randomData = new byte[chunkSize];
        RandomNumberGenerator.Fill(randomData);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var content = new ByteArrayContent(randomData);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                using var response = await _httpClient.PostAsync(UploadUrl, content, ct);

                if (response.IsSuccessStatusCode)
                {
                    onBytesSent(chunkSize);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Upload worker error: {ex.Message}");
                // Small delay before retry
                await Task.Delay(100, ct);
            }
        }
    }

    /// <summary>
    /// Reports progress to subscribers
    /// </summary>
    private DateTime _lastProgressUpdate = DateTime.MinValue;
    private void ReportProgress(SpeedTestPhase phase, double progressPercent, double speedOrLatency, string message)
    {
        // Throttle progress updates
        if ((DateTime.Now - _lastProgressUpdate).TotalMilliseconds < ProgressUpdateIntervalMs &&
            phase != SpeedTestPhase.Complete)
        {
            return;
        }

        _lastProgressUpdate = DateTime.Now;

        var progress = new SpeedTestProgress
        {
            Phase = phase,
            ProgressPercent = progressPercent,
            CurrentSpeedMbps = phase == SpeedTestPhase.Download || phase == SpeedTestPhase.Upload ? speedOrLatency : 0,
            CurrentLatencyMs = phase == SpeedTestPhase.Latency ? speedOrLatency : 0,
            StatusMessage = message
        };

        ProgressChanged?.Invoke(progress);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
    }
}
