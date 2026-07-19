using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Throughput.Helpers;

namespace Throughput.Services;

/// <summary>
/// Details of an available newer release on GitHub.
/// </summary>
public sealed record UpdateInfo(
    Version Version,
    string Tag,
    string ReleaseName,
    string Notes,
    string HtmlUrl,
    string? AssetName,
    string? AssetUrl,
    long AssetSize,
    string? Sha256);

/// <summary>
/// Checks GitHub Releases for a newer version, downloads the right asset for how
/// the app was installed, and applies the update.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private const string ReleasesLatestUrl =
        "https://api.github.com/repos/HakkanShah/Throughput/releases/latest";

    private readonly HttpClient _http;
    private bool _disposed;

    public UpdateService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(100) };
        _http.DefaultRequestHeaders.Add("User-Agent", "Throughput-Updater");
        _http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    }

    /// <summary>
    /// True when the app was installed via the Inno Setup installer (which drops
    /// unins000.exe beside the exe), false for the portable single-file build.
    /// </summary>
    public static bool IsInstalledMode =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    private static string DownloadDir =>
        Path.Combine(Path.GetTempPath(), "Throughput", "update");

    /// <summary>
    /// Queries the latest GitHub release. Returns an <see cref="UpdateInfo"/> when a
    /// newer version is available, or null when already up to date. Throws on
    /// network/parse failures so callers can distinguish "up to date" from "failed".
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(ReleasesLatestUrl, ct);
        resp.EnsureSuccessStatusCode();

        string json = await resp.Content.ReadAsStringAsync(ct);
        return EvaluateRelease(json, AppInfo.Current, IsInstalledMode);
    }

    /// <summary>
    /// Pure parse + version-compare + asset-selection over a GitHub "latest release"
    /// JSON payload. Returns an <see cref="UpdateInfo"/> when <paramref name="json"/>
    /// describes a version newer than <paramref name="current"/>, else null. No I/O -
    /// this is the unit-testable core of <see cref="CheckForUpdateAsync"/>.
    /// </summary>
    internal static UpdateInfo? EvaluateRelease(string json, Version current, bool installedMode)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        var match = Regex.Match(tag, @"\d+\.\d+\.\d+");
        if (!match.Success) return null;

        var latest = Normalize(Version.Parse(match.Value));
        if (latest <= Normalize(current)) return null; // already current

        string name = root.TryGetProperty("name", out var n) ? n.GetString() ?? tag : tag;
        string notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
        string htmlUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";

        var (assetName, assetUrl, assetSize, sha) = PickAsset(root, installedMode);

        return new UpdateInfo(latest, tag, name, notes, htmlUrl, assetName, assetUrl, assetSize, sha);
    }

    /// <summary>
    /// Selects the release asset matching the install mode (Setup vs Portable),
    /// falling back to any .exe asset.
    /// </summary>
    internal static (string? Name, string? Url, long Size, string? Sha) PickAsset(JsonElement root, bool installedMode)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            return (null, null, 0, null);

        string preferred = installedMode ? "setup" : "portable";
        JsonElement? fallback = null;

        foreach (var asset in assets.EnumerateArray())
        {
            string aName = asset.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
            if (!aName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

            fallback ??= asset;
            if (aName.Contains(preferred, StringComparison.OrdinalIgnoreCase))
                return Extract(asset);
        }

        return fallback.HasValue ? Extract(fallback.Value) : (null, null, 0, null);

        static (string?, string?, long, string?) Extract(JsonElement a)
        {
            string name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            string url = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
            long size = a.TryGetProperty("size", out var s) && s.TryGetInt64(out var sv) ? sv : 0;
            string? digest = a.TryGetProperty("digest", out var d) ? d.GetString() : null;
            return (name, url, size, digest);
        }
    }

    /// <summary>
    /// Downloads the update asset to a temp folder, reporting 0-100 progress, and
    /// verifies its SHA-256 against the digest GitHub reports. Returns the file path.
    /// </summary>
    public async Task<string> DownloadAsync(UpdateInfo info, IProgress<double>? progress, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(info.AssetUrl) || string.IsNullOrEmpty(info.AssetName))
            throw new InvalidOperationException("No downloadable asset for this release.");

        Directory.CreateDirectory(DownloadDir);
        string path = Path.Combine(DownloadDir, info.AssetName);

        using (var resp = await _http.GetAsync(info.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? info.AssetSize;

            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                readTotal += read;
                if (total > 0) progress?.Report(Math.Min(100, (double)readTotal / total * 100));
            }
        }

        await VerifyHashAsync(path, info.Sha256, ct);
        return path;
    }

    internal static async Task VerifyHashAsync(string path, string? expected, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(expected)) return;

        // digest is "sha256:<hex>"
        string hex = expected.Contains(':') ? expected[(expected.IndexOf(':') + 1)..] : expected;

        using var sha = SHA256.Create();
        await using var fs = File.OpenRead(path);
        byte[] hash = await sha.ComputeHashAsync(fs, ct);
        string actual = Convert.ToHexString(hash);

        if (!string.Equals(actual, hex, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The downloaded update failed its integrity check.");
    }

    /// <summary>
    /// Launches the update process (does not exit the app - the caller should shut
    /// down afterward). Installed builds run the installer silently; portable builds
    /// hand off to a helper that swaps the exe once this process exits.
    /// </summary>
    public static void ApplyUpdate(string downloadedPath)
    {
        if (IsInstalledMode)
        {
            // Inno Setup in-place upgrade; Restart Manager closes and relaunches us.
            Process.Start(new ProcessStartInfo
            {
                FileName = downloadedPath,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /NORESTART",
                UseShellExecute = true // allow a UAC prompt if the user installed to Program Files
            });
        }
        else
        {
            LaunchPortableSwap(downloadedPath);
        }
    }

    /// <summary>
    /// Writes and starts a detached helper that waits for this process to exit,
    /// overwrites the running exe with the freshly downloaded one, and relaunches it.
    /// </summary>
    private static void LaunchPortableSwap(string newExePath)
    {
        string? target = Environment.ProcessPath;
        if (string.IsNullOrEmpty(target))
            throw new InvalidOperationException("Could not determine the running executable path.");

        int pid = Environment.ProcessId;
        string script = Path.Combine(DownloadDir, "apply-update.cmd");

        string contents =
            "@echo off\r\n" +
            ":waitloop\r\n" +
            $"tasklist /FI \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul\r\n" +
            "if not errorlevel 1 (\r\n" +
            "  timeout /t 1 /nobreak >nul\r\n" +
            "  goto waitloop\r\n" +
            ")\r\n" +
            $"copy /Y \"{newExePath}\" \"{target}\" >nul\r\n" +
            $"start \"\" \"{target}\"\r\n" +
            "del \"%~f0\"\r\n";

        File.WriteAllText(script, contents);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    internal static Version Normalize(Version v) =>
        new(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));

    public void Dispose()
    {
        if (_disposed) return;
        _http.Dispose();
        _disposed = true;
    }
}
