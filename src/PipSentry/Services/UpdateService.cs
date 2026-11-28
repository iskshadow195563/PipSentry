using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PipSentry.Services;

public sealed record ReleaseAsset(string Name, string Url, long Size);

public sealed record ReleaseInfo(
    Version Version,
    string Tag,
    string Title,
    string Notes,
    string PageUrl,
    ReleaseAsset? Msi,
    ReleaseAsset? Portable);

public static class UpdateService
{
    private const string LatestRelease = "https://api.github.com/repos/iskshadow195563/PipSentry/releases/latest";
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PipSentry", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static Version CurrentVersion => typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0);

    public static bool IsNewer(Version current, Version latest) => Normalize(latest) > Normalize(current);

    public static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build));

    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;

        var text = tag.Trim();
        if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase)) text = text[1..];

        var dash = text.IndexOf('-');
        if (dash > 0) text = text[..dash];

        var parts = text.Split('.');
        if (parts.Length < 2) return false;
        if (!int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor)) return false;
        var build = 0;
        if (parts.Length > 2 && !int.TryParse(parts[2], out build)) build = 0;

        version = new Version(major, minor, build);
        return true;
    }

    public static async Task<ReleaseInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync(LatestRelease, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;

        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
        if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;

        var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() ?? string.Empty : string.Empty;
        if (!TryParseTag(tag, out var version)) return null;

        var title = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? tag : tag;
        var notes = root.TryGetProperty("body", out var bodyElement) ? bodyElement.GetString() ?? string.Empty : string.Empty;
        var page = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() ?? string.Empty : string.Empty;

        ReleaseAsset? msi = null;
        ReleaseAsset? portable = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var assetName = asset.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                var assetUrl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
                var size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                if (assetUrl.Length == 0) continue;

                if (assetName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) msi = new ReleaseAsset(assetName, assetUrl, size);
                else if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) portable = new ReleaseAsset(assetName, assetUrl, size);
            }
        }

        return new ReleaseInfo(version, tag, title, notes, page, msi, portable);
    }

    public static async Task<string> DownloadAsync(ReleaseAsset asset, string targetPath, IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(asset.Url, UriKind.Absolute, out var uri) || !IsTrustedDownload(uri))
            throw new InvalidOperationException("untrusted download url");

        var directory = System.IO.Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory)) System.IO.Directory.CreateDirectory(directory);

        using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? asset.Size;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = System.IO.File.Create(targetPath);

        var buffer = new byte[81920];
        long copied = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            copied += read;
            if (total > 0) progress?.Report(Math.Min(100, copied * 100.0 / total));
        }

        if (asset.Size > 0 && copied != asset.Size)
            throw new InvalidOperationException($"size mismatch ({copied} != {asset.Size})");

        progress?.Report(100);
        return targetPath;
    }

    /// <summary>Starts the installer for the downloaded file. Returns false when nothing could be started.</summary>
    public static bool LaunchInstaller(string file, bool isMsi)
    {
        if (isMsi)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("msiexec.exe", "/i \"" + file + "\"")
            {
                UseShellExecute = true,
            });
            return true;
        }

        var target = Environment.ProcessPath;
        if (string.IsNullOrEmpty(target)) return false;

        var script = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PipSentry-update.cmd");
        var pid = Environment.ProcessId;
        var lines = new[]
        {
            "@echo off",
            ":wait",
            "tasklist /FI \"PID eq " + pid + "\" 2>nul | find \"" + pid + "\" >nul",
            "if not errorlevel 1 (",
            "  timeout /t 1 /nobreak >nul",
            "  goto wait",
            ")",
            "copy /Y \"" + file + "\" \"" + target + "\" >nul",
            "start \"\" \"" + target + "\"",
            "del \"%~f0\"",
        };
        System.IO.File.WriteAllLines(script, lines);

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c \"" + script + "\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        });
        return true;
    }

    public static bool IsTrustedDownload(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));
}