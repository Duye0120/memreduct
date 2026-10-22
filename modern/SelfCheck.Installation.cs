using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MemReduct.Fluent;

public sealed partial class MainWindow
{
    private static void CheckInstallationAndUpdates(string directory)
    {
        Require(Installation.IsInstalledPath(Installation.ExecutablePath), "The fixed installed path is recognized");
        Require(!Installation.IsInstalledPath(Path.Combine(Path.GetTempPath(), "MemReduct.Fluent.exe")), "Portable paths cannot request persistent elevation");
        Require(!Installation.IsInstalledPath(Path.Combine(Installation.DirectoryPath + "-other", "MemReduct.Fluent.exe")), "Sibling paths are not trusted installations");
        Require(Installation.GrantsUntrustedWrite("S-1-5-32-545", FileSystemRights.WriteData), "Users may not modify elevated binaries");
        Require(!Installation.GrantsUntrustedWrite("S-1-5-32-545", FileSystemRights.ReadAndExecute), "Read/execute ACLs remain allowed");
        Require(!Installation.GrantsUntrustedWrite("S-1-5-32-544", FileSystemRights.FullControl), "Administrators can maintain the installation");

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
            assets = new[] { new { name = "MemReductFluent-1.0.1-win-x64-setup.exe", browser_download_url = url, digest = hash, size = 100 } }
        }));
        var expected = UpdateService.Repository + "/releases/download/fluent-v1.0.1/MemReductFluent-1.0.1-win-x64-setup.exe";
        using var release = Release("fluent-v1.0.1", expected, "sha256:" + digest);
        Require(UpdateService.ParseRelease(release.RootElement, new Version(1, 0, 0, 0))?.Version == new Version(1, 0, 1), "New stable release is recognized");
        Require(UpdateService.ParseRelease(release.RootElement, new Version(1, 0, 1, 0)) is null, "Same-version reinstall is not offered");
        Require(UpdateService.ParseRelease(release.RootElement, new Version(2, 0, 0, 0)) is null, "Downgrades are not offered");
        using var upstream = Release("v3.5", expected, "sha256:" + digest);
        Require(UpdateService.ParseRelease(upstream.RootElement, new Version(1, 0, 0)) is null, "Upstream legacy tags are ignored");
        using var preview = Release("fluent-v1.0.1", expected, "sha256:" + digest, true);
        Require(UpdateService.ParseRelease(preview.RootElement, new Version(1, 0, 0)) is null, "Prereleases are ignored");
        foreach (var (url, hash) in new[] { ("https://example.org/setup.exe", "sha256:" + digest), (expected, "") })
        {
            using var invalid = Release("fluent-v1.0.1", url, hash);
            rejected = false;
            try { UpdateService.ParseRelease(invalid.RootElement, new Version(1, 0, 0)); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Foreign download sources and missing digests are rejected");
        }
        Require(UpdateService.AllowedDownloadUri(new Uri(expected)) && !UpdateService.AllowedDownloadUri(new Uri("http://github.com/file"))
            && !UpdateService.AllowedDownloadUri(new Uri("https://github.com.evil.example/file")), "Download redirects require HTTPS and approved GitHub hosts");
        Require(UpdateService.DigestMatches(Convert.FromHexString(digest), digest)
            && !UpdateService.DigestMatches(new byte[32], digest), "Installer hash mismatches are rejected");
    }
}
