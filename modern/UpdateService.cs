using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace MemReduct.Fluent;

internal static class UpdateService
{
    internal const string Repository = UpdatePackage.Repository;
    internal static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version!;
    internal static string DisplayVersion => CurrentVersion.ToString(3);
    private static readonly HttpClient Client = CreateClient();
    internal static string DownloadPath => Path.Combine(Installation.UserDirectory, "Updates", "update.exe");

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MemReductFluent/" + DisplayVersion);
        return client;
    }

    internal static async Task<AppRelease?> CheckAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var response = await Client.GetAsync(UpdatePackage.LatestApi, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        await response.Content.LoadIntoBufferAsync(UpdatePackage.MaximumMetadata, timeout.Token);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        return ParseRelease(json.RootElement, CurrentVersion);
    }

    internal static AppRelease? ParseRelease(JsonElement json, Version current)
        => UpdatePackage.ParseRelease(json.GetRawText(), current);

    internal static Task<string> DownloadAsync(AppRelease release, IProgress<long>? progress = null, CancellationToken cancellationToken = default) =>
        UserDataAccess.RunAsync(() => DownloadCoreAsync(release, progress, cancellationToken));

    private static async Task<string> DownloadCoreAsync(AppRelease release, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(DownloadPath)!;
        Directory.CreateDirectory(directory);
        foreach (var folder in new[] { Installation.UserDirectory, directory })
            if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Update cache cannot be a symbolic link.");
        var target = DownloadPath;
        var partial = target + ".partial";
        // This cache is never executed. The elevated helper independently verifies a protected copy.
        if (File.Exists(target)) File.Delete(target);
        if (File.Exists(partial)) File.Delete(partial);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            using var response = await GetDownloadAsync(release.Download, timeout.Token);
            if (response.Content.Headers.ContentLength is long length && length != release.Size) throw new InvalidDataException(UiText.Get("UpdateHashFailed"));
            await using (var source = await response.Content.ReadAsStreamAsync(timeout.Token))
                await CopyVerifiedAsync(source, partial, release, timeout.Token, progress);
            File.Move(partial, target);
            return target;
        }
        catch { if (File.Exists(partial)) File.Delete(partial); throw; }
    }

    internal static Task CopyVerifiedAsync(Stream source, string destinationPath, AppRelease release, CancellationToken token, IProgress<long>? progress = null)
        => UpdatePackage.CopyVerifiedAsync(source, destinationPath, release, token, progress);

    internal static bool DigestMatches(byte[] hash, string expected) => UpdatePackage.DigestMatches(hash, expected);
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

    internal static async Task RequestInstallAsync(string path)
    {
        if (!Installation.IsInstalled) throw new InvalidOperationException(UiText.Get("InstalledOnly"));
        if (path != DownloadPath) throw new InvalidOperationException();
        HelperTrust.RequireProtected();
        using var process = Process.Start(CreateInstallStartInfo()) ?? throw new InvalidOperationException(UiText.Get("UpdateFailed"));
        await process.WaitForExitAsync();
        if (process.ExitCode == 5) throw new UnauthorizedAccessException(UiText.Get("UpdateSameUser"));
        if (process.ExitCode != 0) throw new InvalidOperationException(UiText.Get("UpdateFailed"));
    }

    internal static ProcessStartInfo CreateInstallStartInfo()
    {
        var start = new ProcessStartInfo(HelperTrust.ExecutablePath) { UseShellExecute = true, Verb = "runas", WorkingDirectory = HelperTrust.DirectoryPath };
        start.ArgumentList.Add("--install-update");
        start.ArgumentList.Add(HelperTrust.UserSid);
        return start;
    }
}
