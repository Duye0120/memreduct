using System.Diagnostics;
using System.Security.Principal;

namespace MemReduct.Fluent;

internal static class ElevationHandoff
{
    internal const string Argument = "--elevation-handoff";
    internal const int WrongAccountExitCode = 2;
    internal static string EventName(Guid request) => @"Local\MemReduct.Fluent.Elevation." + request.ToString("N");
    internal static string TransferEventName(Guid request) => EventName(request) + ".Transfer";

    internal static ProcessStartInfo CreateStartInfo(Guid request, string intent)
    {
        if (intent is not ("" or "--clean")) throw new ArgumentException("Invalid elevation intent.", nameof(intent));
        var start = new ProcessStartInfo(Environment.ProcessPath!)
        { UseShellExecute = true, Verb = "runas", WorkingDirectory = AppContext.BaseDirectory };
        using var identity = WindowsIdentity.GetCurrent();
        start.ArgumentList.Add("--elevated");
        start.ArgumentList.Add(Argument);
        start.ArgumentList.Add(identity.User!.Value);
        start.ArgumentList.Add(request.ToString("N"));
        if (intent.Length != 0) start.ArgumentList.Add(intent);
        return start;
    }

    internal static Guid Validate(string[] arguments, string currentUser, bool elevated)
    {
        var index = Array.IndexOf(arguments, Argument);
        if (index < 0 || index + 2 >= arguments.Length
            || arguments.Count(value => value == Argument) != 1
            || !Guid.TryParseExact(arguments[index + 2], "N", out var request) || request == Guid.Empty)
            throw new ArgumentException("Invalid elevation handoff.");
        if (!elevated || arguments[index + 1] != currentUser)
            throw new UnauthorizedAccessException("Elevation must use the requesting account.");
        return request;
    }

    internal static void WaitForTransfer(Guid request, TimeSpan timeout)
    {
        // The requesting process owns both session-local events; no settings or commands pass through them.
        using var ready = EventWaitHandle.OpenExisting(EventName(request));
        using var transfer = EventWaitHandle.OpenExisting(TransferEventName(request));
        ready.Set();
        if (!transfer.WaitOne(timeout)) throw new TimeoutException("The requesting process did not complete the elevation handoff.");
    }
}
