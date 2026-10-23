using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

namespace MemReduct.Fluent;

internal static class HelperClient
{
    internal static async Task<bool> TryConnectAsync(bool startAuthorizedTask)
    {
        try
        {
            HelperTrust.RequireProtected();
            if (startAuthorizedTask && !await Task.Run(HelperTasks.TryRun)) return false;
            for (var attempt = 0; attempt < (startAuthorizedTask ? 12 : 1); attempt++)
            {
                try { await SendAsync(false, CleanAreas.None, false, 600); return true; }
                catch (Exception error) when (error is IOException or TimeoutException or OperationCanceledException)
                { if (attempt == 11) return false; }
            }
        }
        catch (Exception) { return false; }
        return false;
    }

    internal static async Task ConfigureAsync(string mode)
    {
        HelperTrust.RequireProtected();
        var start = new ProcessStartInfo(HelperTrust.ExecutablePath)
        { UseShellExecute = true, Verb = "runas", WorkingDirectory = HelperTrust.DirectoryPath };
        start.ArgumentList.Add(mode); start.ArgumentList.Add(HelperTrust.UserSid);
        using var process = Process.Start(start) ?? throw new InvalidOperationException(UiText.Get("StartupAdminFailed"));
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException(UiText.Get("StartupAdminFailed"));
    }

    internal static Task<CleanupResult> CleanAsync(CleanAreas areas, bool automatic)
        => SendAsync(true, areas, automatic, 120000);

    private static async Task<CleanupResult> SendAsync(bool clean, CleanAreas areas, bool automatic, int timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        // Identification prevents a counterfeit pipe server from impersonating the UI for user-data writes.
        // GenericWrite also requests CreateNewInstance; request only the rights granted to an ordinary client.
        using var pipe = new NamedPipeClientStream(".", HelperTrust.PipeName, PipeAccessRights.ReadWrite, PipeOptions.Asynchronous,
            TokenImpersonationLevel.Identification, HandleInheritability.None);
        await pipe.ConnectAsync(cancellation.Token);
        HelperTrust.VerifyPeer(pipe, server: true);
        var request = HelperProtocol.Request(clean, areas, automatic, UiText.LanguageTag == "zh-CN");
        await pipe.WriteAsync(request, cancellation.Token);
        var length = BitConverter.ToInt32(await HelperProtocol.ReadAsync(pipe, 4, cancellation.Token), 0);
        var result = HelperProtocol.ParseResponse(await HelperProtocol.ReadAsync(pipe, length, cancellation.Token));
        var allowed = automatic ? MemoryEngine.ForAutomaticCleanup(areas) : areas;
        if ((result.CompletedAreas & ~allowed) != 0) throw new InvalidDataException("Helper returned unexpected cleanup areas.");
        return result;
    }
}
