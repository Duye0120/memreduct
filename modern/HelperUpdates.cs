#if !HELPER
#nullable disable // Shared with the protected Windows .NET Framework helper.
#endif
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace MemReduct.Fluent
{
    internal static class HelperUpdates
    {
        internal static async Task InstallAsync()
        {
            HelperTrust.RequireProtected();
            var installation = Path.GetDirectoryName(HelperTasks.UiExecutable());
            // Version and release metadata come from protected code and GitHub, never from the writable UI.
            AppRelease release;
            using (var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }))
            using (var timeout = new CancellationTokenSource(20000))
            {
                client.MaxResponseContentBufferSize = UpdatePackage.MaximumMetadata;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("MemReductFluent/" + Assembly.GetExecutingAssembly().GetName().Version);
                using (var response = await client.GetAsync(UpdatePackage.LatestApi, timeout.Token).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound) throw new InvalidOperationException(UiText.Get("UpdateNone"));
                    response.EnsureSuccessStatusCode();
                    release = UpdatePackage.ParseRelease(await response.Content.ReadAsStringAsync().ConfigureAwait(false),
                        Assembly.GetExecutingAssembly().GetName().Version);
                }
            }
            if (release == null) throw new InvalidOperationException(UiText.Get("UpdateNone"));
            var directory = Path.Combine(HelperTrust.DirectoryPath, "Updates");
            Directory.CreateDirectory(directory);
            HelperTrust.CheckPath(new DirectoryInfo(directory), false);
            var target = Path.Combine(directory, "update.exe");
            var partial = target + ".partial";
            foreach (var file in new[] { target, partial })
                if (File.Exists(file)) { HelperTrust.CheckPath(new FileInfo(file), false); File.Delete(file); }
            using (var timeout = new CancellationTokenSource(120000))
            using (var source = UserDataAccess.Run(() => new FileStream(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MemReductFluent", "Updates", "update.exe"),
                FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true)))
                await UpdatePackage.CopyVerifiedAsync(source, partial, release, timeout.Token).ConfigureAwait(false);
            File.Move(partial, target);
            HelperTrust.CheckPath(new FileInfo(target), false);
            // Setup is elevated, but its finish/cancel hook reopens the UI with the ordinary shell token.
            using (var process = Process.Start(new ProcessStartInfo(target, "/FROMAPP=1 /DIR=\"" + installation + "\"")
                { UseShellExecute = true, WorkingDirectory = HelperTrust.DirectoryPath }))
                if (process == null) throw new InvalidOperationException(UiText.Get("UpdateFailed"));
        }
    }
}
