using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MemReduct.Fluent;

public sealed partial class MainWindow
{
    private static async Task CheckUpdateCopyAsync(string directory)
    {
        var attempts = 0;
        Require(await App.WaitForWindowAsync(() => ++attempts == 3, TimeSpan.FromSeconds(2)) && attempts == 3,
            "Startup waits for the destination window instead of dropping the first show request");
        Require(!await App.WaitForWindowAsync(() => false, TimeSpan.FromMilliseconds(20)),
            "An unavailable startup window times out and permits a normal launch");
        Require(await App.WaitForWindowAsync(() => true, TimeSpan.Zero), "An already responsive window is accepted immediately");
        var payload = Encoding.UTF8.GetBytes("verified release payload");
        var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var release = new AppRelease(new Version(0, 0, 1), new Uri(UpdateService.Repository), hash, payload.Length);
        var destination = Path.Combine(directory, "update-copy-check.bin");
        if (File.Exists(destination)) File.Delete(destination);
        using (var source = new MemoryStream(payload))
            await UpdateService.CopyVerifiedAsync(source, destination, release, CancellationToken.None);
        Require(File.ReadAllBytes(destination).SequenceEqual(payload), "Verified copy preserves the exact bytes");
        var rejected = false;
        try
        {
            using var source = new MemoryStream(payload);
            await UpdateService.CopyVerifiedAsync(source, destination, release, CancellationToken.None);
        }
        catch (IOException) { rejected = true; }
        Require(rejected && File.ReadAllBytes(destination).SequenceEqual(payload), "Existing files are not overwritten or deleted");
        File.Delete(destination);
        foreach (var bytes in new[] { new byte[payload.Length], payload[..^1], payload.Concat(new byte[] { 0 }).ToArray() })
        {
            rejected = false;
            try
            {
                using var source = new MemoryStream(bytes);
                await UpdateService.CopyVerifiedAsync(source, destination, release, CancellationToken.None);
            }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected && !File.Exists(destination), "Tampered, truncated and oversized copies leave no executable payload");
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        rejected = false;
        try
        {
            using var source = new MemoryStream(payload);
            await UpdateService.CopyVerifiedAsync(source, destination, release, cancellation.Token);
        }
        catch (OperationCanceledException) { rejected = true; }
        Require(rejected && !File.Exists(destination), "Canceled copies remove their partial payload");
    }

    private static void CheckInstallationAndUpdates(string directory)
    {
        // Exercise the installed COM path inside WinUI even when this check runs from a portable build.
        var startupEnabled = StartupRegistration.IsInstalledStartupEnabled();
        File.WriteAllText(Path.Combine(directory, "startup-read.json"), JsonSerializer.Serialize(new { startupEnabled }));
        Require(Installation.IsInstalledPath(Installation.ExecutablePath), "The registered installed path is recognized");
        Require(StartupRegistration.ShouldReuseStartup(true, false, ["app.exe"])
            && StartupRegistration.ShouldReuseStartup(true, false, ["app.exe", "--restart"]), "Manual installed launches reuse existing authorization");
        Require(!StartupRegistration.ShouldReuseStartup(false, false, ["app.exe"])
            && !StartupRegistration.ShouldReuseStartup(true, true, ["app.exe"]), "Portable and already elevated processes do not relaunch");
        foreach (var argument in new[] { "--self-check", "--elevated", "--minimized", "--clean", "--install-update", "--configure-startup", "--import-settings" })
            Require(!StartupRegistration.ShouldReuseStartup(true, false, ["app.exe", argument]),
                "Helper, background and explicit action launches cannot recurse or replay through the startup task");
        var customDirectory = Installation.ResolveDirectory(@"D:\Apps\内存工具\");
        Require(customDirectory == @"D:\Apps\内存工具"
            && Installation.MatchesExecutable(@"d:\apps\内存工具\MemReduct.Fluent.exe", customDirectory),
            "Custom installation paths support another drive, Unicode and case-insensitive matching");
        Require(!Installation.MatchesExecutable(@"D:\Apps\内存工具-other\MemReduct.Fluent.exe", customDirectory)
            && !Installation.MatchesExecutable(@"D:\Apps\内存工具\other.exe", customDirectory), "Only the exact registered executable is installed");
        foreach (var invalidDirectory in new[] { @"relative\folder", @"\\server\share\app" })
        {
            var invalidRejected = false;
            try { Installation.ResolveDirectory(invalidDirectory); } catch (InvalidDataException) { invalidRejected = true; }
            Require(invalidRejected, "Relative and network installation paths are rejected");
        }
        Require(Installation.ResolveDirectory(null) == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MemReduct Fluent"),
            "Existing default installations retain their path");
        Require(!Installation.IsInstalledPath(Path.Combine(Path.GetTempPath(), "MemReduct.Fluent.exe")), "Portable paths cannot request persistent elevation");
        Require(!Installation.IsInstalledPath(Path.Combine(Installation.DirectoryPath + "-other", "MemReduct.Fluent.exe")), "Sibling paths are not trusted installations");
        Require(Installation.GrantsUntrustedWrite("S-1-5-32-545", FileSystemRights.WriteData), "Users may not modify elevated binaries");
        Require(!Installation.GrantsUntrustedWrite("S-1-5-32-545", FileSystemRights.ReadAndExecute), "Read/execute ACLs remain allowed");
        Require(!Installation.GrantsUntrustedWrite("S-1-5-32-544", FileSystemRights.FullControl), "Administrators can maintain the installation");
        Require(Installation.GrantsUntrustedReplacement("S-1-5-32-545", FileSystemRights.DeleteSubdirectoriesAndFiles)
            && Installation.GrantsUntrustedReplacement("S-1-5-32-545", FileSystemRights.ChangePermissions), "Writable ancestors cannot replace elevated installations");
        Require(!Installation.GrantsUntrustedReplacement("S-1-5-32-545", FileSystemRights.CreateDirectories | FileSystemRights.ReadAndExecute),
            "Creating sibling directories does not grant replacement of the installation");

        const string sid = "S-1-5-21-111-222-333-1001";
        var xml = StartupRegistration.CreateTaskXml(sid, Installation.ExecutablePath);
        File.WriteAllText(Path.Combine(directory, "startup-task.xml"), xml);
        Require(StartupRegistration.MatchesTask(xml, sid, Installation.ExecutablePath), "Task runs the exact executable at this user's interactive logon");
        Require(!StartupRegistration.MatchesTask(xml.Replace("HighestAvailable", "LeastPrivilege"), sid, Installation.ExecutablePath), "Non-elevated tasks are not mistaken for elevated startup");
        Require(!StartupRegistration.MatchesTask(xml.Replace("--elevated --minimized", "--clean"), sid, Installation.ExecutablePath), "Changed task arguments are rejected");
        Require(!StartupRegistration.MatchesTask(xml, sid + "1", Installation.ExecutablePath), "Other users' tasks are rejected");
        Require(xml.Contains("PT0S") && xml.Contains("<DisallowStartIfOnBatteries>false"), "Tray startup is not time-limited or AC-only");

        // All migration files live in this self-check output, never beside the user's executable.
        var migration = Path.Combine(directory, "settings-import-check");
        Directory.CreateDirectory(migration);
        var source = Path.Combine(migration, Guid.NewGuid() + "-source.json");
        var destination = Path.Combine(migration, Guid.NewGuid() + "-target.json");
        var original = "{\"Language\":\"en-US\",\"Threshold\":87,\"GameMode\":false,\"NotifyAfterCleanup\":true}";
        File.WriteAllText(source, original);
        SettingsStore.ImportIfMissing(source, destination);
        Require(File.ReadAllText(destination) == original && File.ReadAllText(source) == original, "Import copies settings without altering the source");
        File.WriteAllText(source, "invalid");
        SettingsStore.ImportIfMissing(source, destination);
        Require(File.ReadAllText(destination) == original, "Upgrade never overwrites an existing profile");
        var rejected = false;
        var invalidTarget = Path.Combine(migration, "invalid.json");
        try { SettingsStore.ImportIfMissing(source, invalidTarget); } catch (JsonException) { rejected = true; }
        Require(rejected && !File.Exists(invalidTarget), "Invalid imported JSON is rejected before writing a profile");

        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("installer fixture"))).ToLowerInvariant();
        JsonDocument Release(string tag, string url, string hash, bool prerelease = false) => JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            tag_name = tag, draft = false, prerelease,
            assets = new[] { new { name = $"MemReductFluent-{tag.Replace("fluent-v", "")}-win-x64-setup.exe", browser_download_url = url, digest = hash, size = 100 } }
        }));
        var expected = UpdateService.Repository + "/releases/download/fluent-v0.0.2/MemReductFluent-0.0.2-win-x64-setup.exe";
        using var release = Release("fluent-v0.0.2", expected, "sha256:" + digest);
        Require(UpdateService.ParseRelease(release.RootElement, new Version(0, 0, 1, 0))?.Version == new Version(0, 0, 2), "New stable release is recognized");
        Require(UpdateService.ParseRelease(release.RootElement, new Version(0, 0, 2, 0)) is null, "Same-version reinstall is not offered");
        Require(UpdateService.ParseRelease(release.RootElement, new Version(0, 0, 3, 0)) is null, "Downgrades are not offered");
        foreach (var tag in new[] { "fluent-v1.0.0", "fluent-v1.0.1", "fluent-v1.0.2" })
        {
            using var legacy = Release(tag, expected, "sha256:" + digest);
            Require(UpdateService.ParseRelease(legacy.RootElement, new Version(0, 0, 1, 0)) is null,
                "Releases from before the version reset are not offered");
        }
        using var upstream = Release("v3.5", expected, "sha256:" + digest);
        Require(UpdateService.ParseRelease(upstream.RootElement, new Version(0, 0, 1)) is null, "Upstream legacy tags are ignored");
        using var preview = Release("fluent-v0.0.2", expected, "sha256:" + digest, true);
        Require(UpdateService.ParseRelease(preview.RootElement, new Version(0, 0, 1)) is null, "Prereleases are ignored");
        foreach (var (url, hash) in new[] { ("https://example.org/setup.exe", "sha256:" + digest), (expected, "") })
        {
            using var invalid = Release("fluent-v0.0.2", url, hash);
            rejected = false;
            try { UpdateService.ParseRelease(invalid.RootElement, new Version(0, 0, 1)); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Foreign download sources and missing digests are rejected");
        }
        Require(UpdateService.AllowedDownloadUri(new Uri(expected)) && !UpdateService.AllowedDownloadUri(new Uri("http://github.com/file"))
            && !UpdateService.AllowedDownloadUri(new Uri("https://github.com.evil.example/file")), "Download redirects require HTTPS and approved GitHub hosts");
        Require(UpdateService.DigestMatches(Convert.FromHexString(digest), digest)
            && !UpdateService.DigestMatches(new byte[32], digest), "Installer hash mismatches are rejected");
    }
}
