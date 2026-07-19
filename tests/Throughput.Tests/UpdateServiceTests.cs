using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Throughput.Services;
using Xunit;

namespace Throughput.Tests;

public class UpdateServiceTests
{
    private static string ReleaseJson(
        string tag = "v3.0.1",
        string name = "v3.0.1 - Taskbar fix",
        string body = "Some release notes.",
        bool withAssets = true)
    {
        string assets = withAssets
            ? """
              "assets": [
                { "name": "Throughput-Setup-3.0.1.exe", "browser_download_url": "https://example.com/Throughput-Setup-3.0.1.exe", "size": 61070598, "digest": "sha256:aaa" },
                { "name": "Throughput-Portable-3.0.1.exe", "browser_download_url": "https://example.com/Throughput-Portable-3.0.1.exe", "size": 66098155, "digest": "sha256:bbb" }
              ]
              """
            : @"""assets"": []";

        return $$"""
        {
          "tag_name": "{{tag}}",
          "name": "{{name}}",
          "body": "{{body}}",
          "html_url": "https://github.com/HakkanShah/Throughput/releases/tag/{{tag}}",
          {{assets}}
        }
        """;
    }

    // ---- version comparison ----

    [Fact]
    public void EvaluateRelease_NewerVersion_ReturnsInfo()
    {
        var info = UpdateService.EvaluateRelease(ReleaseJson(), new Version(3, 0, 0), installedMode: false);

        Assert.NotNull(info);
        Assert.Equal(new Version(3, 0, 1), info!.Version);
        Assert.Equal("v3.0.1", info.Tag);
    }

    [Fact]
    public void EvaluateRelease_EqualVersion_ReturnsNull()
    {
        var info = UpdateService.EvaluateRelease(ReleaseJson(), new Version(3, 0, 1), installedMode: false);
        Assert.Null(info);
    }

    [Fact]
    public void EvaluateRelease_OlderRelease_ReturnsNull()
    {
        var info = UpdateService.EvaluateRelease(ReleaseJson(tag: "v3.0.1"), new Version(4, 0, 0), installedMode: false);
        Assert.Null(info);
    }

    [Fact]
    public void EvaluateRelease_IgnoresAssemblyRevisionComponent()
    {
        // Current build 3.0.1.5 should still equal release 3.0.1 (revision ignored).
        var info = UpdateService.EvaluateRelease(ReleaseJson(tag: "v3.0.1"), new Version(3, 0, 1, 5), installedMode: false);
        Assert.Null(info);
    }

    [Theory]
    [InlineData("v3.2.0", 3, 2, 0)]
    [InlineData("Throughput-v1.0.0", 1, 0, 0)]
    [InlineData("3.5.7", 3, 5, 7)]
    public void EvaluateRelease_ParsesVersionFromVariousTags(string tag, int maj, int min, int build)
    {
        var info = UpdateService.EvaluateRelease(ReleaseJson(tag: tag), new Version(0, 1, 0), installedMode: false);

        Assert.NotNull(info);
        Assert.Equal(new Version(maj, min, build), info!.Version);
    }

    [Fact]
    public void EvaluateRelease_NonNumericTag_ReturnsNull()
    {
        var info = UpdateService.EvaluateRelease(ReleaseJson(tag: "latest"), new Version(1, 0, 0), installedMode: false);
        Assert.Null(info);
    }

    // ---- metadata ----

    [Fact]
    public void EvaluateRelease_PopulatesNotesAndUrl()
    {
        var info = UpdateService.EvaluateRelease(ReleaseJson(body: "Hello notes"), new Version(1, 0, 0), installedMode: false);

        Assert.NotNull(info);
        Assert.Equal("Hello notes", info!.Notes);
        Assert.Contains("github.com/HakkanShah/Throughput", info.HtmlUrl);
        Assert.Equal("v3.0.1 - Taskbar fix", info.ReleaseName);
    }

    // ---- asset selection ----

    [Fact]
    public void EvaluateRelease_InstalledMode_PicksSetupAsset()
    {
        var info = UpdateService.EvaluateRelease(ReleaseJson(), new Version(1, 0, 0), installedMode: true);

        Assert.NotNull(info);
        Assert.Contains("Setup", info!.AssetName);
        Assert.Equal("sha256:aaa", info.Sha256);
        Assert.Equal(61070598, info.AssetSize);
    }

    [Fact]
    public void EvaluateRelease_PortableMode_PicksPortableAsset()
    {
        var info = UpdateService.EvaluateRelease(ReleaseJson(), new Version(1, 0, 0), installedMode: false);

        Assert.NotNull(info);
        Assert.Contains("Portable", info!.AssetName);
        Assert.Equal("sha256:bbb", info.Sha256);
    }

    [Fact]
    public void EvaluateRelease_NoAssets_ReturnsInfoWithoutAsset()
    {
        var info = UpdateService.EvaluateRelease(ReleaseJson(withAssets: false), new Version(1, 0, 0), installedMode: false);

        Assert.NotNull(info);
        Assert.Null(info!.AssetUrl);
        Assert.Null(info.AssetName);
    }

    [Fact]
    public void PickAsset_FallsBackToAnyExe_WhenNoModeMatch()
    {
        const string json = """
        { "assets": [ { "name": "Throughput-3.0.1.exe", "browser_download_url": "https://example.com/x.exe", "size": 10, "digest": "sha256:ccc" } ] }
        """;
        using var doc = JsonDocument.Parse(json);

        var (name, url, size, sha) = UpdateService.PickAsset(doc.RootElement, installedMode: true);

        Assert.Equal("Throughput-3.0.1.exe", name);
        Assert.Equal("https://example.com/x.exe", url);
        Assert.Equal(10, size);
        Assert.Equal("sha256:ccc", sha);
    }

    [Fact]
    public void PickAsset_IgnoresNonExeAssets()
    {
        const string json = """
        { "assets": [ { "name": "notes.txt", "browser_download_url": "https://example.com/notes.txt", "size": 5 } ] }
        """;
        using var doc = JsonDocument.Parse(json);

        var (name, url, _, _) = UpdateService.PickAsset(doc.RootElement, installedMode: false);

        Assert.Null(name);
        Assert.Null(url);
    }

    // ---- normalization ----

    [Fact]
    public void Normalize_FillsMissingComponentsWithZero()
    {
        Assert.Equal(new Version(3, 0, 0), UpdateService.Normalize(new Version(3, 0)));
        Assert.Equal(new Version(3, 2, 1), UpdateService.Normalize(new Version(3, 2, 1, 9)));
    }

    // ---- integrity check ----

    [Fact]
    public async Task VerifyHashAsync_MatchingDigest_DoesNotThrow()
    {
        string path = Path.GetTempFileName();
        try
        {
            byte[] data = Encoding.UTF8.GetBytes("throughput update payload");
            await File.WriteAllBytesAsync(path, data);
            string digest = "sha256:" + Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

            await UpdateService.VerifyHashAsync(path, digest, CancellationToken.None); // should not throw
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task VerifyHashAsync_WrongDigest_Throws()
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes("real content"));
            string wrong = "sha256:" + new string('0', 64);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => UpdateService.VerifyHashAsync(path, wrong, CancellationToken.None));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task VerifyHashAsync_NoDigest_SkipsCheck()
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes("x"));
            await UpdateService.VerifyHashAsync(path, null, CancellationToken.None); // no-op, should not throw
        }
        finally { File.Delete(path); }
    }
}
