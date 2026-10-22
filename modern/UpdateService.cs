using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MemReduct.Fluent;

internal sealed record AppRelease(Version Version, Uri Download, string Sha256, long Size);

internal static class UpdateService
{
    internal const string Repository = "https://github.com/Duye0120/memreduct";
    internal static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version!;
    internal static string DisplayVersion => CurrentVersion.ToString(3);
    private const long MaximumDownload = 512L * 1024 * 1024;
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MemReductFluent/" + DisplayVersion);
        return client;
    }

    internal static async Task<AppRelease?> CheckAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var response = await Client.GetAsync("https://api.github.com/repos/Duye0120/memreduct/releases/latest", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        await response.Content.LoadIntoBufferAsync(1024 * 1024, timeout.Token);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        return ParseRelease(json.RootElement, CurrentVersion);
    }

    internal static AppRelease? ParseRelease(JsonElement json, Version current)
    {
        if (json.GetProperty("draft").GetBoolean() || json.GetProperty("prerelease").GetBoolean()) return null;
        var tag = json.GetProperty("tag_name").GetString()!;
        var match = Regex.Match(tag, @"^fluent-v(\d+\.\d+\.\d+)$", RegexOptions.CultureInvariant);
        if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var version)) return null;
        if (new Version(version.Major, version.Minor, version.Build, 0) <= current) return null;
        var name = $"MemReductFluent-{version}-win-x64-setup.exe";
        var assets = json.GetProperty("assets").EnumerateArray().Where(asset => asset.GetProperty("name").GetString() == name).ToArray();
        if (assets.Length != 1) throw new InvalidDataException(UiText.Get("UpdateAssetInvalid"));
        var asset = assets[0];
        var expected = $"{Repository}/releases/download/{tag}/{name}";
        var digest = asset.TryGetProperty("digest", out var value) ? value.GetString() : null;
        var size = asset.GetProperty("size").GetInt64();
        if (asset.GetProperty("browser_download_url").GetString() != expected || size is <= 0 or > MaximumDownload
            || digest is null || !Regex.IsMatch(digest, @"^sha256:[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException(UiText.Get("UpdateAssetInvalid"));
        return new(version, new Uri(expected), digest[7..], size);
    }

    internal static async Task<string> DownloadAsync(AppRelease release)
    {
        if (!MemoryEngine.IsAdministrator) throw new UnauthorizedAccessException(UiText.Get("PermissionRequired"));
        Installation.RequireProtectedInstall();
        var directory = Path.Combine(Installation.DirectoryPath, "Updates");
        Directory.CreateDirectory(directory);
        Installation.RequireProtectedPath(new DirectoryInfo(directory));
        // One protected cache slot; never execute installers from a user-writable download directory.
        var target = Path.Combine(directory, "update.exe");
        var partial = target + ".partial";
        if (File.Exists(target)) { Installation.RequireProtectedPath(new FileInfo(target)); File.Delete(target); }
        if (File.Exists(partial)) { Installation.RequireProtectedPath(new FileInfo(partial)); File.Delete(partial); }
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        try
        {
            using var response = await GetDownloadAsync(release.Download, timeout.Token);
            if (response.Content.Headers.ContentLength is long length && length != release.Size) throw new InvalidDataException(UiText.Get("UpdateHashFailed"));
            await using (var source = await response.Content.ReadAsStreamAsync(timeout.Token))
            await using (var destination = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await source.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    total += count;
                    if (total > release.Size) throw new InvalidDataException(UiText.Get("UpdateHashFailed"));
                    hash.AppendData(buffer, 0, count);
                    await destination.WriteAsync(buffer.AsMemory(0, count), timeout.Token);
                }
                if (total != release.Size || !DigestMatches(hash.GetHashAndReset(), release.Sha256))
                    throw new InvalidDataException(UiText.Get("UpdateHashFailed"));
            }
            File.Move(partial, target);
            return target;
        }
        catch { if (File.Exists(partial)) File.Delete(partial); throw; }
    }

    internal static bool DigestMatches(byte[] hash, string expected) => CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(expected));
    internal static bool AllowedDownloadUri(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0
        && uri.Host is "github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com";

    private static async Task<HttpResponseMessage> GetDownloadAsync(Uri uri, CancellationToken token)
    {
        for (var redirect = 0; redirect < 5; redirect++)
        {
            if (!AllowedDownloadUri(uri)) throw new InvalidDataException(UiText.Get("UpdateAssetInvalid"));
            var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                var location = response.Headers.Location;
                response.Dispose();
                if (location is null) throw new InvalidDataException(UiText.Get("UpdateAssetInvalid"));
                uri = new Uri(uri, location);
                continue;
            }
            try { response.EnsureSuccessStatusCode(); return response; }
            catch { response.Dispose(); throw; }
        }
        throw new InvalidDataException(UiText.Get("UpdateAssetInvalid"));
    }

    internal static void StartInstaller(string path)
    {
        Installation.RequireProtectedInstall();
        if (path != Path.Combine(Installation.DirectoryPath, "Updates", "update.exe")) throw new InvalidOperationException();
        var start = new ProcessStartInfo(path) { UseShellExecute = true, Verb = "runas", WorkingDirectory = Installation.DirectoryPath };
        start.ArgumentList.Add("/FROMAPP=1");
        using var process = Process.Start(start) ?? throw new InvalidOperationException(UiText.Get("UpdateFailed"));
    }
}
