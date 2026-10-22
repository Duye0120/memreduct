using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;

namespace MemReduct.Fluent;

internal static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MemReduct.Fluent";
    private const string TaskPrefix = "MemReduct.Fluent.";
    private static string UserSid => WindowsIdentity.GetCurrent().User!.Value;
    private static string TaskName => TaskPrefix + UserSid;
    private static string Command => "\"" + Environment.ProcessPath + "\" --minimized";

    public static bool IsEnabled()
    {
        if (Installation.IsInstalled)
        {
            using var com = new ComScope();
            dynamic folder = Connect(com);
            object? found = FindTask(com, folder, TaskName);
            if (found is null) return false;
            dynamic task = found;
            return task.Enabled && MatchesTask((string)task.Xml, UserSid, Installation.ExecutablePath);
        }
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
    }
    public static async Task SetEnabledAsync(bool enabled)
    {
        if (Installation.IsInstalled)
        {
            if (MemoryEngine.IsAdministrator) ConfigureInstalled(enabled, UserSid);
            else
            {
                Installation.RequireProtectedInstall();
                var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas", WorkingDirectory = Installation.DirectoryPath };
                start.ArgumentList.Add("--configure-startup");
                start.ArgumentList.Add(enabled ? "on" : "off");
                start.ArgumentList.Add(UserSid);
                using var process = Process.Start(start) ?? throw new InvalidOperationException(UiText.Get("StartupFailed"));
                await process.WaitForExitAsync();
                if (process.ExitCode != 0) throw new InvalidOperationException(UiText.Get("StartupAdminFailed"));
            }
            if (IsEnabled() != enabled) throw new InvalidOperationException(UiText.Get("StartupFailed"));
            return;
        }
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        if (enabled) key.SetValue(ValueName, Command, RegistryValueKind.String);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    internal static void ConfigureInstalled(bool enabled, string expectedUser)
    {
        if (!MemoryEngine.IsAdministrator || expectedUser != UserSid) throw new UnauthorizedAccessException("Use the same administrator account that is signed in.");
        Installation.RequireProtectedInstall();
        using var com = new ComScope();
        dynamic folder = Connect(com);
        dynamic? existing = FindTask(com, folder, TaskName);
        if (existing is not null && !MatchesTask((string)existing.Xml, UserSid, Installation.ExecutablePath))
            throw new InvalidOperationException("A different task already uses this name.");
        if (enabled)
            com.Keep(folder.RegisterTask(TaskName, CreateTaskXml(UserSid, Installation.ExecutablePath), 6, UserSid, null, 3,
                $"O:BAG:BAD:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGX;;;{UserSid})"));
        else if (existing is not null) folder.DeleteTask(TaskName, 0);
        // Only retire this app's legacy Run entry after the scheduled-task operation succeeds.
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        if (key?.GetValue(ValueName) is string value && value.EndsWith("\\MemReduct.Fluent.exe\" --minimized", StringComparison.OrdinalIgnoreCase))
            key.DeleteValue(ValueName, false);
    }

    internal static string CreateTaskXml(string sid, string executable)
    {
        _ = new SecurityIdentifier(sid);
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        XElement E(string name, params object[] values) => new(ns + name, values);
        return new XDocument(E("Task", new XAttribute("version", "1.2"),
            E("RegistrationInfo", E("Description", "Mem Reduct Fluent: elevated tray startup for this user.")),
            E("Triggers", E("LogonTrigger", E("Enabled", "true"), E("UserId", sid))),
            E("Principals", E("Principal", new XAttribute("id", "User"), E("UserId", sid), E("LogonType", "InteractiveToken"), E("RunLevel", "HighestAvailable"))),
            E("Settings", E("MultipleInstancesPolicy", "IgnoreNew"), E("DisallowStartIfOnBatteries", "false"), E("StopIfGoingOnBatteries", "false"),
                E("ExecutionTimeLimit", "PT0S")),
            E("Actions", new XAttribute("Context", "User"), E("Exec", E("Command", executable), E("Arguments", "--elevated --minimized"),
                E("WorkingDirectory", System.IO.Path.GetDirectoryName(executable)!))))).ToString();
    }

    internal static bool MatchesTask(string xml, string sid, string executable)
    {
        var root = XDocument.Parse(xml).Root!;
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        var actions = root.Element(ns + "Actions")?.Elements().ToArray();
        var principal = root.Element(ns + "Principals")?.Element(ns + "Principal");
        var triggers = root.Element(ns + "Triggers")?.Elements().ToArray();
        return actions?.Length == 1 && actions[0].Name == ns + "Exec"
            && triggers?.Length == 1 && triggers[0].Name == ns + "LogonTrigger"
            && (string?)triggers[0].Element(ns + "UserId") == sid
            && (string?)triggers[0].Element(ns + "Enabled") == "true"
            && string.Equals((string?)actions[0].Element(ns + "Command"), executable, StringComparison.OrdinalIgnoreCase)
            && (string?)actions[0].Element(ns + "Arguments") == "--elevated --minimized"
            && string.Equals((string?)actions[0].Element(ns + "WorkingDirectory"), System.IO.Path.GetDirectoryName(executable), StringComparison.OrdinalIgnoreCase)
            && (string?)principal?.Element(ns + "UserId") == sid
            && (string?)principal?.Element(ns + "LogonType") == "InteractiveToken"
            && (string?)principal?.Element(ns + "RunLevel") == "HighestAvailable";
    }

    internal static void RunInstalled()
    {
        Installation.RequireProtectedInstall();
        using var com = new ComScope();
        dynamic folder = Connect(com);
        object? found = FindTask(com, folder, TaskName);
        if (found is null) throw new InvalidOperationException(UiText.Get("StartupFailed"));
        dynamic task = found;
        if (!task.Enabled || !MatchesTask((string)task.Xml, UserSid, Installation.ExecutablePath)) throw new InvalidOperationException(UiText.Get("StartupFailed"));
        com.Keep(task.Run(null));
    }

    private static dynamic Connect(ComScope com)
    {
        dynamic service = com.Keep(Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!);
        service.Connect();
        return com.Keep(service.GetFolder("\\"));
    }
    private static dynamic? FindTask(ComScope com, dynamic folder, string name)
    {
        try { return com.Keep(folder.GetTask(name)); }
        catch (COMException error) when (error.HResult == unchecked((int)0x80070002)) { return null; }
    }
    private sealed class ComScope : IDisposable
    {
        private readonly List<object> _objects = [];
        internal dynamic Keep(object value) { _objects.Add(value); return value; }
        public void Dispose() { for (var i = _objects.Count - 1; i >= 0; i--) Marshal.ReleaseComObject(_objects[i]); }
    }
}
