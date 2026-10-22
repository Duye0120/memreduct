using System.Security.Principal;

namespace MemReduct.Fluent;

public sealed partial class MainWindow
{
    private async Task CheckElevationHandoffAsync()
    {
        var initialGameMode = _settings.GameMode;
        var initialConfirmation = _settings.ConfirmCleanup;
        _elevating = true;
        try
        {
            RefreshOptions();
            SetGameMode(!initialGameMode);
            Require(!SaveCleanupConfirmation(!initialConfirmation) && _settings.GameMode == initialGameMode
                && _settings.ConfirmCleanup == initialConfirmation && !SettingsHost.IsEnabled && !PaneToggleButton.IsEnabled
                && !CleanButton.IsEnabled && !ElevateButton.IsEnabled,
                "Pending elevation prevents duplicate actions and preference changes before transfer");
        }
        finally { _elevating = false; RefreshOptions(); }
        Require(SettingsHost.IsEnabled && PaneToggleButton.IsEnabled, "The original interface becomes usable again after unsuccessful elevation");
        using var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User!.Value;
        var request = Guid.NewGuid();
        var start = ElevationHandoff.CreateStartInfo(request, "--clean");
        var arguments = start.ArgumentList.ToArray();
        Require(start.FileName == Environment.ProcessPath && start.UseShellExecute && start.Verb == "runas"
            && arguments[^1] == "--clean", "Elevation launches only this application with the explicit cleanup intent");
        Require(ElevationHandoff.Validate(arguments, sid, true) == request, "The matching elevated caller passes request validation");
        foreach (var pair in new[] { (User: "S-1-5-18", Elevated: true), (User: sid, Elevated: false) })
        {
            var rejected = false;
            try { ElevationHandoff.Validate(arguments, pair.User, pair.Elevated); }
            catch (UnauthorizedAccessException) { rejected = true; }
            Require(rejected, "A different account or an ordinary token cannot acknowledge elevation");
        }
        foreach (var malformed in new[]
        {
            Array.Empty<string>(), [ElevationHandoff.Argument, sid],
            [ElevationHandoff.Argument, sid, "not-a-guid"],
            [ElevationHandoff.Argument, sid, Guid.Empty.ToString("N")],
            arguments.Concat(new[] { ElevationHandoff.Argument, sid, request.ToString("N") }).ToArray()
        })
        {
            var rejected = false;
            try { ElevationHandoff.Validate(malformed, sid, true); }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, "Malformed or duplicate handoff arguments are rejected before opening an event");
        }
        using (var ready = new EventWaitHandle(false, EventResetMode.ManualReset, ElevationHandoff.EventName(request), out var created))
        using (var transfer = new EventWaitHandle(false, EventResetMode.ManualReset, ElevationHandoff.TransferEventName(request), out var transferCreated))
        {
            Require(created && transferCreated && !ready.WaitOne(0) && !transfer.WaitOne(0), "Each handoff starts with new, unsignaled events");
            var waiting = Task.Run(() => ElevationHandoff.WaitForTransfer(request, TimeSpan.FromSeconds(3)));
            Require(await App.WaitUntilAsync(() => ready.WaitOne(0), TimeSpan.FromSeconds(2)) && !waiting.IsCompleted,
                "Readiness alone cannot continue elevated startup before the original window approves transfer");
            transfer.Set();
            await waiting;
            transfer.Reset();
            var timedOut = false;
            try { ElevationHandoff.WaitForTransfer(request, TimeSpan.Zero); }
            catch (TimeoutException) { timedOut = true; }
            Require(timedOut, "Canceled or timed-out parents cannot authorize delayed cleanup");
        }
        var expired = false;
        try { ElevationHandoff.WaitForTransfer(request, TimeSpan.Zero); }
        catch (WaitHandleCannotBeOpenedException) { expired = true; }
        Require(expired, "An abandoned handoff cannot create a new event or continue startup");
    }
}
