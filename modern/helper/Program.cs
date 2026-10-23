using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using MemReduct.Fluent;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--self-check") { Check(); return 0; }
            if (args.Length == 1 && args[0] == "--verify") { HelperTrust.RequireProtected(); Check(); return 0; }
            if (args.Length == 1 && args[0] == "--migrate-startup") { HelperTasks.Configure("migrate", HelperTrust.UserSid); return 0; }
            if (args.Length == 1 && args[0] == "--launch-ui") { HelperTrust.RequireProtected(); UserDataAccess.StartInstalledUi(); return 0; }
            if (args.Length != 2 || args[1] != HelperTrust.UserSid || !MemoryEngine.IsAdministrator) return 5;
            HelperTrust.RequireProtected();
            if (args[0] == "--serve") Serve().GetAwaiter().GetResult();
            else if (args[0] == "--install-update") HelperUpdates.InstallAsync().GetAwaiter().GetResult();
            else HelperTasks.Configure(args[0], args[1]);
            return 0;
        }
        catch { return 1; }
    }
    private static async Task Serve()
    {
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier("S-1-5-2"), PipeAccessRights.FullControl, AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(HelperTrust.UserSid), PipeAccessRights.ReadWrite, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier("S-1-5-18"), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier("S-1-5-32-544"), PipeAccessRights.FullControl, AccessControlType.Allow));
        while (true)
        {
            using (var pipe = new NamedPipeServerStream(HelperTrust.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, security))
            {
                await pipe.WaitForConnectionAsync().ConfigureAwait(false);
                try
                {
                    HelperTrust.VerifyPeer(pipe, false);
                    bool clean, automatic, chinese; CleanAreas areas;
                    using (var timeout = new CancellationTokenSource(5000))
                    using (timeout.Token.Register(() => pipe.Dispose()))
                        HelperProtocol.ParseRequest(await HelperProtocol.ReadAsync(pipe, 12, timeout.Token).ConfigureAwait(false), out clean, out areas, out automatic, out chinese);
                    UiText.Chinese = chinese;
                    CleanupResult result;
                    try
                    {
                        result = clean && areas != CleanAreas.None ? MemoryEngine.Clean(areas, automatic)
                            : new CleanupResult(0, new string[0], CleanAreas.None);
                    }
                    catch (Exception error) { result = new CleanupResult(0, new[] { error.Message }, CleanAreas.None); }
                    var response = HelperProtocol.Response(result);
                    using (var timeout = new CancellationTokenSource(5000))
                    using (timeout.Token.Register(() => pipe.Dispose()))
                    {
                        var length = BitConverter.GetBytes(response.Length);
                        await pipe.WriteAsync(length, 0, length.Length, timeout.Token).ConfigureAwait(false);
                        await pipe.WriteAsync(response, 0, response.Length, timeout.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException || error is OperationCanceledException
                    || error is ArgumentException || error is System.ComponentModel.Win32Exception || error is ObjectDisposedException) { }
            }
        }
    }
    private static void Check()
    {
        var targets = System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false);
        if (targets.Length != 1 || ((System.Runtime.Versioning.TargetFrameworkAttribute)targets[0]).FrameworkName != ".NETFramework,Version=v4.8")
            throw new Exception("The helper must target .NET Framework 4.8 for modern HTTPS defaults.");
        bool clean, automatic, chinese; CleanAreas areas;
        HelperProtocol.ParseRequest(HelperProtocol.Request(true, (CleanAreas)255, true, true), out clean, out areas, out automatic, out chinese);
        if (!clean || !automatic || !chinese || (areas & MemoryEngine.ManualOnlyAreas) != 0) throw new Exception("Automatic cleanup filter failed.");
        var original = new CleanupResult(123, new[] { "中文 / error" }, CleanAreas.WorkingSets);
        var result = HelperProtocol.ParseResponse(HelperProtocol.Response(original));
        if (result.FreedBytes != 123 || result.Errors[0] != original.Errors[0] || result.CompletedAreas != original.CompletedAreas) throw new Exception("Protocol round trip failed.");
        var sid = "S-1-5-21-1-2-3-1001";
        if (!HelperTasks.Matches(HelperTasks.CreateXml(sid, false), sid) || !HelperTasks.Matches(HelperTasks.CreateXml(sid, true), sid)) throw new Exception("Task contract failed.");
        var payload = System.Text.Encoding.UTF8.GetBytes("verified update fixture");
        string digest;
        using (var hash = System.Security.Cryptography.SHA256.Create()) digest = BitConverter.ToString(hash.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
        var json = "{\"draft\":false,\"prerelease\":false,\"tag_name\":\"fluent-v0.0.2\",\"assets\":[{\"name\":\"MemReductFluent-0.0.2-win-x64-setup.exe\",\"browser_download_url\":\""
            + UpdatePackage.Repository + "/releases/download/fluent-v0.0.2/MemReductFluent-0.0.2-win-x64-setup.exe\",\"digest\":\"sha256:" + digest + "\",\"size\":" + payload.Length + "}]}";
        var release = UpdatePackage.ParseRelease(json, new Version(0, 0, 1, 0));
        if (release == null || release.Version != new Version(0, 0, 2) || UpdatePackage.ParseRelease(json, new Version(0, 0, 2, 0)) != null)
            throw new Exception("Update manifest contract failed.");
        var destination = Path.Combine(Path.GetTempPath(), "MemReduct-update-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var source = new MemoryStream(payload)) UpdatePackage.CopyVerifiedAsync(source, destination, release, CancellationToken.None).GetAwaiter().GetResult();
            if (new FileInfo(destination).Length != payload.Length) throw new Exception("Verified update copy failed.");
            File.Delete(destination);
            var rejected = false;
            try { using (var source = new MemoryStream(new byte[payload.Length])) UpdatePackage.CopyVerifiedAsync(source, destination, release, CancellationToken.None).GetAwaiter().GetResult(); }
            catch (InvalidDataException) { rejected = true; }
            if (!rejected || File.Exists(destination)) throw new Exception("An unverified update was retained.");
        }
        finally { if (File.Exists(destination)) File.Delete(destination); }
    }
}
