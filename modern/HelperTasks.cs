#if !HELPER
#nullable disable // Shared C# 6 sources target the Windows .NET Framework helper.
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;
using Microsoft.Win32;

namespace MemReduct.Fluent
{
    internal static class HelperTasks
    {
        internal const string Prefix = "MemReduct.Fluent.Helper.";
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "MemReduct.Fluent";
        internal static string UiExecutable()
        {
            using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var key = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{5B9D5C89-F01C-4AB6-A593-DA69B3835832}_is1"))
            {
                var path = key == null ? null : key.GetValue("InstallLocation") as string;
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || path.StartsWith(@"\\") || path.Length < 4)
                    throw new InvalidDataException("The application installation is not registered.");
                return Path.Combine(Path.GetFullPath(path), "MemReduct.Fluent.exe");
            }
        }
        private static string RunCommand { get { return "\"" + UiExecutable() + "\" --minimized"; } }
        internal static bool StartupEnabled()
        {
            using (var com = new ComScope())
            {
                dynamic folder = Connect(com);
                dynamic task = Find(com, folder, Prefix + HelperTrust.UserSid);
                if (task == null || !task.Enabled || !Matches((string)task.Xml, HelperTrust.UserSid) || !LogonEnabled((string)task.Xml)) return false;
                return UserDataAccess.Run(() => { using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return key != null && string.Equals(key.GetValue(RunValue) as string, RunCommand, StringComparison.OrdinalIgnoreCase); });
            }
        }
        internal static bool TryRun()
        {
            HelperTrust.RequireProtected();
            using (var com = new ComScope())
            {
                dynamic folder = Connect(com);
                dynamic task = Find(com, folder, Prefix + HelperTrust.UserSid);
                if (task == null) return false;
                if (!task.Enabled || !Matches((string)task.Xml, HelperTrust.UserSid)) throw new UnauthorizedAccessException("The helper task does not match this account and protected executable.");
                com.Keep(task.Run(null));
                return true;
            }
        }
        internal static void Configure(string mode, string sid)
        {
            if (!MemoryEngine.IsAdministrator || sid != HelperTrust.UserSid) throw new UnauthorizedAccessException("Authorize with the currently signed-in administrator account.");
            if (mode != "authorize" && mode != "on" && mode != "off" && mode != "migrate") throw new ArgumentException("Invalid helper setup request.");
            HelperTrust.RequireProtected();
            using (var com = new ComScope())
            {
                dynamic folder = Connect(com);
                var name = Prefix + sid;
                dynamic oldTask = Find(com, folder, name);
                string oldXml = oldTask == null ? null : (string)oldTask.Xml;
                if (oldXml != null && !Matches(oldXml, sid)) throw new UnauthorizedAccessException("Another task uses the helper name.");
                dynamic legacy = Find(com, folder, "MemReduct.Fluent." + sid);
                var migrate = legacy != null && legacy.Enabled && LegacyMatches((string)legacy.Xml, sid, UiExecutable());
                if (mode == "migrate" && !migrate) return;
                var enabled = mode == "on" || mode == "migrate" || (mode == "authorize" && (migrate || oldXml != null && LogonEnabled(oldXml)));
                var changeRun = mode != "authorize" || migrate;
                var oldRun = UserDataAccess.Run(() => { using (var key = Registry.CurrentUser.OpenSubKey(RunKey)) return key == null ? null : key.GetValue(RunValue) as string; });
                var registered = false;
                try
                {
                    Register(com, folder, name, CreateXml(sid, enabled), sid);
                    registered = true;
                    if (changeRun) SetRun(enabled ? RunCommand : null);
                }
                catch
                {
                    if (registered) { if (oldXml == null) folder.DeleteTask(name, 0); else Register(com, folder, name, oldXml, sid); }
                    if (changeRun) SetRun(oldRun);
                    throw;
                }
                if (migrate) { if (legacy.State == 4) legacy.Stop(0); folder.DeleteTask("MemReduct.Fluent." + sid, 0); }
                if (mode != "off")
                {
                    dynamic task = Find(com, folder, name);
                    com.Keep(task.Run(null));
                }
            }
        }
        private static void SetRun(string command)
        {
            UserDataAccess.Run(() => { using (var key = Registry.CurrentUser.CreateSubKey(RunKey, true))
                { if (command == null) key.DeleteValue(RunValue, false); else key.SetValue(RunValue, command, RegistryValueKind.String); } });
        }
        private static void Register(ComScope com, dynamic folder, string name, string xml, string sid)
        { com.Keep(folder.RegisterTask(name, xml, 6, sid, null, 3, "O:BAG:BAD:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGX;;;" + sid + ")")); }
        internal static string CreateXml(string sid, bool logon)
        {
            new SecurityIdentifier(sid);
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            // Build through XML nodes so account and path values cannot inject task actions.
            var task = new XElement(ns + "Task", new XAttribute("version", "1.2"),
                new XElement(ns + "RegistrationInfo", new XElement(ns + "Description", "Mem Reduct: protected memory cleanup helper for this account.")),
                new XElement(ns + "Triggers", new XElement(ns + "LogonTrigger", new XElement(ns + "Enabled", logon ? "true" : "false"), new XElement(ns + "UserId", sid))),
                new XElement(ns + "Principals", new XElement(ns + "Principal", new XAttribute("id", "User"), new XElement(ns + "UserId", sid),
                    new XElement(ns + "LogonType", "InteractiveToken"), new XElement(ns + "RunLevel", "HighestAvailable"))),
                new XElement(ns + "Settings", new XElement(ns + "MultipleInstancesPolicy", "IgnoreNew"), new XElement(ns + "DisallowStartIfOnBatteries", "false"),
                    new XElement(ns + "StopIfGoingOnBatteries", "false"), new XElement(ns + "ExecutionTimeLimit", "PT0S")),
                new XElement(ns + "Actions", new XAttribute("Context", "User"), new XElement(ns + "Exec", new XElement(ns + "Command", HelperTrust.ExecutablePath),
                    new XElement(ns + "Arguments", "--serve " + sid), new XElement(ns + "WorkingDirectory", HelperTrust.DirectoryPath))));
            return new XDocument(task).ToString();
        }
        internal static bool Matches(string xml, string sid)
        { return MatchesAction(xml, sid, HelperTrust.ExecutablePath, "--serve " + sid, false); }
        internal static bool LegacyMatches(string xml, string sid, string executable)
        { return MatchesAction(xml, sid, executable, "--elevated --minimized", true); }
        private static bool MatchesAction(string xml, string sid, string executable, string arguments, bool requireLogon)
        {
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            var root = XDocument.Parse(xml).Root;
            var actions = root.Element(ns + "Actions")?.Elements().ToArray();
            var principals = root.Element(ns + "Principals")?.Elements().ToArray();
            var triggers = root.Element(ns + "Triggers")?.Elements().ToArray();
            return actions?.Length == 1 && actions[0].Name == ns + "Exec" && principals?.Length == 1 && triggers?.Length == 1
                && triggers[0].Name == ns + "LogonTrigger" && MatchesUser((string)triggers[0].Element(ns + "UserId"), sid)
                && ((string)triggers[0].Element(ns + "Enabled") == null || (string)triggers[0].Element(ns + "Enabled") == "true" || (string)triggers[0].Element(ns + "Enabled") == "false")
                && (!requireLogon || LogonEnabled(xml))
                && string.Equals((string)actions[0].Element(ns + "Command"), executable, StringComparison.OrdinalIgnoreCase)
                && (string)actions[0].Element(ns + "Arguments") == arguments
                && string.Equals((string)actions[0].Element(ns + "WorkingDirectory"), Path.GetDirectoryName(executable), StringComparison.OrdinalIgnoreCase)
                && MatchesUser((string)principals[0].Element(ns + "UserId"), sid) && (string)principals[0].Element(ns + "LogonType") == "InteractiveToken"
                && (string)principals[0].Element(ns + "RunLevel") == "HighestAvailable";
        }
        private static bool MatchesUser(string value, string sid)
        {
            if (value == sid) return true;
            if (string.IsNullOrEmpty(value) || value.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase)) return false;
            // Task Scheduler rewrites logon-trigger SIDs as account names when registering XML.
            try { return ((SecurityIdentifier)new NTAccount(value).Translate(typeof(SecurityIdentifier))).Value == sid; }
            catch (IdentityNotMappedException) { return false; }
            catch (ArgumentException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return false; }
        }
        private static bool LogonEnabled(string xml)
        {
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            // Enabled defaults to true; Windows omits the node when serializing enabled triggers.
            return (string)XDocument.Parse(xml).Root.Element(ns + "Triggers")?.Element(ns + "LogonTrigger")?.Element(ns + "Enabled") != "false";
        }
        private static dynamic Connect(ComScope com)
        {
            dynamic service = com.Keep(Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")));
            service.Connect(); return com.Keep(service.GetFolder("\\"));
        }
        private static dynamic Find(ComScope com, dynamic folder, string name)
        {
            try { return com.Keep(folder.GetTask(name)); }
            catch (Exception error) when (error.HResult == unchecked((int)0x80070002)) { return null; }
        }
        private sealed class ComScope : IDisposable
        {
            private readonly List<object> _objects = new List<object>();
            internal dynamic Keep(object value) { _objects.Add(value); return value; }
            public void Dispose() { for (var i = _objects.Count - 1; i >= 0; i--) Marshal.ReleaseComObject(_objects[i]); }
        }
    }
}
