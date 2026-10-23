using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Security.Principal;
using System.Xml.Linq;

namespace MemReduct.Fluent;

public sealed partial class MainWindow
{
    private static async Task CheckUserDataAccessAsync(string directory)
    {
        static (string? Sid, bool Administrator, bool Impersonating, TokenImpersonationLevel Level) Identity()
        {
            using var identity = WindowsIdentity.GetCurrent();
            using var thread = WindowsIdentity.GetCurrent(true);
            return (identity.User?.Value, new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator), thread is not null, identity.ImpersonationLevel);
        }
        var original = Identity();
        var synchronous = UserDataAccess.Run(Identity);
        var asynchronous = await UserDataAccess.RunAsync(async () =>
        {
            await Task.Delay(10).ConfigureAwait(false);
            File.WriteAllText(Path.Combine(directory, "async-user-access.txt"), "ordinary file access after await");
            return Identity();
        });
        Require(synchronous.Sid == original.Sid && !synchronous.Administrator
            && asynchronous.Sid == original.Sid && !asynchronous.Administrator && synchronous.Impersonating && asynchronous.Impersonating
            && synchronous.Level >= TokenImpersonationLevel.Impersonation && asynchronous.Level >= TokenImpersonationLevel.Impersonation
            && File.ReadAllText(Path.Combine(directory, "async-user-access.txt")) == "ordinary file access after await",
            "User-data callbacks retain the same account and a usable ordinary file-access token across await");
        var failed = false;
        try { await UserDataAccess.RunAsync<int>(async () => { await Task.Yield(); throw new InvalidOperationException("fixture"); }); }
        catch (InvalidOperationException error) when (error.Message == "fixture") { failed = true; }
        Require(failed && Identity() == original, "User-data failures restore the original caller identity");
        File.WriteAllText(Path.Combine(directory, "user-data-token-check.json"), JsonSerializer.Serialize(new
        { callerElevated = original.Administrator, synchronousElevated = synchronous.Administrator, asynchronousElevated = asynchronous.Administrator }));

        var target = Path.Combine(directory, "atomic-settings-check.json");
        var settings = new AppSettings { Threshold = 72, Language = "en-US" };
        File.WriteAllText(target + ".tmp", "Existing file must survive");
        SettingsStore.Save(settings, target);
        Require(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(target))!.Threshold == 72
            && File.ReadAllText(target + ".tmp") == "Existing file must survive", "Settings use a fresh temporary file and preserve unrelated existing files");
        var originalJson = File.ReadAllText(target);
        settings.Threshold = 83;
        using (var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var rejected = false;
            try { SettingsStore.Save(settings, target); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { rejected = true; }
            Require(rejected && File.ReadAllText(target) == originalJson, "A failed atomic replace leaves the previous settings intact");
        }
        SettingsStore.Save(settings, target);
        Require(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(target))!.Threshold == 83
            && Directory.GetFiles(directory, "atomic-settings-check.json.*.tmp").Length == 0,
            "Settings save succeeds after retry and removes only its own temporary files");
        Require(SettingsStore.Load(target).Threshold == 83, "Normal settings load uses the same bounded user-data path");
        var oversized = Path.Combine(directory, "oversized-settings.json");
        File.WriteAllText(oversized, new string(' ', 65537));
        var oversizedRejected = false;
        try { _ = SettingsStore.Load(oversized); } catch (InvalidDataException) { oversizedRejected = true; }
        Require(oversizedRejected && new FileInfo(oversized).Length == 65537, "Oversized settings are rejected before JSON allocation without modifying the file");
    }

    private static async Task CheckUpdateCopyAsync(string directory)
    {
        using (var canceled = new CancellationTokenSource())
        {
            canceled.Cancel();
            var checkCanceled = false;
            try { await UpdateService.CheckAsync(canceled.Token); }
            catch (OperationCanceledException) { checkCanceled = true; }
            Require(checkCanceled, "Canceled release checks stop before starting a network request");
        }
        var attempts = 0;
        Require(await App.WaitUntilAsync(() => ++attempts == 3, TimeSpan.FromSeconds(2)) && attempts == 3,
            "Startup waits for the destination window instead of dropping the first show request");
        Require(!await App.WaitUntilAsync(() => false, TimeSpan.FromMilliseconds(20)),
            "An unavailable startup window times out and permits a normal launch");
        Require(await App.WaitUntilAsync(() => true, TimeSpan.Zero), "An already responsive window is accepted immediately");
        var payload = Enumerable.Range(0, 200000).Select(x => (byte)x).ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var release = new AppRelease(new Version(0, 0, 1), new Uri(UpdateService.Repository), hash, payload.Length);
        var destination = Path.Combine(directory, "update-copy-check.bin");
        if (File.Exists(destination)) File.Delete(destination);
        var progress = new List<long>();
        using (var source = new MemoryStream(payload))
            await UpdateService.CopyVerifiedAsync(source, destination, release, CancellationToken.None, new CheckProgress(progress.Add));
        Require(File.ReadAllBytes(destination).SequenceEqual(payload), "Verified copy preserves the exact bytes");
        Require(progress.Count > 2 && progress.Count <= 102 && progress[0] == 0 && progress[^1] == payload.Length
            && progress.SequenceEqual(progress.OrderBy(x => x)), "Download progress is bounded, monotonic and completes after verification");
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
        using var midCopyCancellation = new CancellationTokenSource();
        rejected = false;
        try
        {
            using var source = new MemoryStream(payload);
            await UpdateService.CopyVerifiedAsync(source, destination, release, midCopyCancellation.Token,
                new CheckProgress(bytes => { if (bytes > 0) midCopyCancellation.Cancel(); }));
        }
        catch (OperationCanceledException) { rejected = true; }
        Require(rejected && !File.Exists(destination), "Canceling after a partial write removes the incomplete file");
    }

    private sealed class CheckProgress(Action<long> report) : IProgress<long>
    {
        public void Report(long value) => report(value);
    }

    private static void CheckHelperProtocol()
    {
        HelperProtocol.ParseRequest(HelperProtocol.Request(true, (CleanAreas)255, true, true), out var clean, out var areas, out var automatic, out var chinese);
        Require(clean && automatic && chinese && (areas & MemoryEngine.ManualOnlyAreas) == 0, "Helper independently strips starred automatic areas");
        var result = HelperProtocol.ParseResponse(HelperProtocol.Response(new CleanupResult(42, ["中文 / error"], CleanAreas.WorkingSets)));
        Require(result.FreedBytes == 42 && result.CompletedAreas == CleanAreas.WorkingSets && result.Errors.SequenceEqual(["中文 / error"]), "Bounded helper results round-trip");
        foreach (var invalid in new[] { new byte[0], new byte[13], HelperProtocol.Request(false, CleanAreas.None, false, false) })
        {
            if (invalid.Length == 12) invalid[5] = 3;
            var rejected = false;
            try { HelperProtocol.ParseRequest(invalid, out _, out _, out _, out _); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Malformed helper requests and unknown operations are rejected");
        }
        foreach (var invalid in new[] { new byte[19], new byte[HelperProtocol.MaximumResponse + 1], HelperProtocol.Response(new CleanupResult(0, [], CleanAreas.None)).Concat(new byte[] { 0 }).ToArray() })
        {
            var rejected = false;
            try { HelperProtocol.ParseResponse(invalid); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Truncated, oversized and trailing helper responses are rejected");
        }
    }

    private static void CheckInstallationAndUpdates(string directory)
    {
        var installStart = UpdateService.CreateInstallStartInfo();
        Require(installStart.FileName == HelperTrust.ExecutablePath && installStart.WorkingDirectory == HelperTrust.DirectoryPath
            && installStart.UseShellExecute && installStart.Verb == "runas"
            && installStart.ArgumentList.SequenceEqual(new[] { "--install-update", HelperTrust.UserSid }),
            "Updates elevate only the protected helper, including for a user-writable D-drive installation");
        // Exercise the installed COM path inside WinUI even when this check runs from a portable build.
        var startupEnabled = HelperTasks.StartupEnabled();
        File.WriteAllText(Path.Combine(directory, "startup-read.json"), JsonSerializer.Serialize(new { startupEnabled }));
        Require(Installation.IsInstalledPath(Installation.ExecutablePath), "The registered installed path is recognized");
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
        var xml = HelperTasks.CreateXml(sid, true);
        File.WriteAllText(Path.Combine(directory, "startup-task.xml"), xml);
        Require(HelperTasks.Matches(xml, sid), "Task runs the exact executable at this user's interactive logon");
        Require(!HelperTasks.Matches(xml.Replace("HighestAvailable", "LeastPrivilege"), sid), "Non-elevated tasks are not mistaken for elevated startup");
        Require(!HelperTasks.Matches(xml.Replace("--serve " + sid, "--clean"), sid), "Changed task arguments are rejected");
        Require(!HelperTasks.Matches(xml, sid + "1"), "Other users' tasks are rejected");
        Require(xml.Contains("PT0S") && xml.Contains("<DisallowStartIfOnBatteries>false"), "Tray startup is not time-limited or AC-only");

        Require(HelperTasks.Matches(HelperTasks.CreateXml(sid, false), sid) && !xml.Contains(Installation.ExecutablePath), "Authorization is reusable on demand; no task elevates the D-drive UI");
        using (var identity = WindowsIdentity.GetCurrent())
        {
            var normalized = HelperTasks.CreateXml(identity.User!.Value, true);
            XNamespace taskNs = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            var taskXml = System.Xml.Linq.XDocument.Parse(normalized);
            var trigger = taskXml.Root!.Element(taskNs + "Triggers")!.Element(taskNs + "LogonTrigger")!;
            trigger.Element(taskNs + "UserId")!.Value = identity.Name;
            trigger.Element(taskNs + "Enabled")!.Remove();
            Require(HelperTasks.Matches(taskXml.ToString(), identity.User.Value), "Windows-normalized account names and omitted default Enabled nodes retain the same SID boundary");
        }
        CheckHelperProtocol();

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
        JsonDocument Release(string tag, string url, string hash, bool prerelease = false, long size = 100, int count = 1) => JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            tag_name = tag, draft = false, prerelease,
            assets = Enumerable.Repeat(new { name = $"MemReductFluent-{tag.Replace("fluent-v", "")}-win-x64-setup.exe", browser_download_url = url, digest = hash, size }, count).ToArray()
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
        foreach (var (size, count) in new[] { (0L, 1), (UpdatePackage.MaximumDownload + 1, 1), (100L, 0), (100L, 2) })
        {
            using var invalid = Release("fluent-v0.0.2", expected, "sha256:" + digest, size: size, count: count);
            rejected = false;
            try { UpdateService.ParseRelease(invalid.RootElement, new Version(0, 0, 1)); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Both runtimes reject missing/duplicate installers and invalid asset sizes");
        }
        Require(UpdateService.AllowedDownloadUri(new Uri(expected)) && !UpdateService.AllowedDownloadUri(new Uri("http://github.com/file"))
            && !UpdateService.AllowedDownloadUri(new Uri("https://github.com.evil.example/file")), "Download redirects require HTTPS and approved GitHub hosts");
        Require(UpdateService.DigestMatches(Convert.FromHexString(digest), digest)
            && !UpdateService.DigestMatches(new byte[32], digest), "Installer hash mismatches are rejected");
    }
}
