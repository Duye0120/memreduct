#if !HELPER
#nullable disable // Shared C# 6 sources target the Windows .NET Framework helper.
#endif
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace MemReduct.Fluent
{
    internal static class HelperTrust
    {
        internal static string DirectoryPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MemReduct Helper"); } }
        internal static string ExecutablePath { get { return Path.Combine(DirectoryPath, "MemReduct.Helper.exe"); } }
        internal static string UserSid { get { using (var identity = WindowsIdentity.GetCurrent()) return identity.User.Value; } }
        internal static string PipeName { get { return "MemReduct.Fluent.Helper." + UserSid + "." + Process.GetCurrentProcess().SessionId; } }
        internal static void RequireProtected()
        {
            var directory = new DirectoryInfo(DirectoryPath);
            CheckPath(directory, false);
            for (var parent = directory.Parent; parent != null; parent = parent.Parent) CheckPath(parent, true);
            CheckPath(new FileInfo(ExecutablePath), false);
            CheckPath(new FileInfo(ExecutablePath + ".config"), false);
            // The helper loads only framework assemblies; no adjacent writable DLLs or nested payloads are allowed.
            foreach (var entry in directory.EnumerateFileSystemInfos()) CheckPath(entry, false);
        }
        internal static void CheckPath(FileSystemInfo entry, bool ancestor)
        {
            if (!entry.Exists || (entry.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Helper path is missing or contains a link: " + entry.FullName);
            var directory = entry as DirectoryInfo;
            FileSystemSecurity acl = directory != null ? (FileSystemSecurity)directory.GetAccessControl() : ((FileInfo)entry).GetAccessControl();
            if (!Trusted(acl.GetOwner(typeof(SecurityIdentifier)).Value)) throw new UnauthorizedAccessException("Helper owner is not protected: " + entry.FullName);
            foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                var rights = ancestor ? FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions
                    | FileSystemRights.TakeOwnership | FileSystemRights.WriteAttributes | FileSystemRights.WriteExtendedAttributes
                    : FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
                if (rule.AccessControlType == AccessControlType.Allow && (rule.PropagationFlags & PropagationFlags.InheritOnly) == 0
                    && !Trusted(rule.IdentityReference.Value) && (rule.FileSystemRights & rights) != 0)
                    throw new UnauthorizedAccessException("Helper path is writable by an unprivileged account: " + entry.FullName);
            }
        }
        private static bool Trusted(string sid)
        { return sid == "S-1-5-18" || sid == "S-1-5-32-544" || sid == "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464"; }

        internal static void VerifyPeer(PipeStream pipe, bool server)
        {
            uint id;
            if (!(server ? GetNamedPipeServerProcessId(pipe.SafePipeHandle, out id) : GetNamedPipeClientProcessId(pipe.SafePipeHandle, out id)))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            uint session;
            if (!ProcessIdToSessionId(id, out session) || session != Process.GetCurrentProcess().SessionId) throw new UnauthorizedAccessException("Helper session mismatch.");
            using (var process = OpenProcess(0x1000, false, id))
            {
                SafeAccessTokenHandle token;
                if (process.IsInvalid || !OpenProcessToken(process, 8, out token)) throw new Win32Exception(Marshal.GetLastWin32Error());
                using (token)
                using (var identity = new WindowsIdentity(token.DangerousGetHandle()))
                {
                    if (identity.User.Value != UserSid) throw new UnauthorizedAccessException("Helper account mismatch.");
                    if (server)
                    {
                        int elevated, returned;
                        if (!GetTokenInformation(token, 20, out elevated, 4, out returned) || elevated == 0) throw new UnauthorizedAccessException("Helper is not elevated.");
                        var buffer = new StringBuilder(32768); var size = buffer.Capacity;
                        if (!QueryFullProcessImageName(process, 0, buffer, ref size)
                            || !string.Equals(buffer.ToString(), ExecutablePath, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Unexpected helper executable.");
                    }
                }
            }
        }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint id);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint id);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ProcessIdToSessionId(uint id, out uint session);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, uint id);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref int size);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int type, out int data, int size, out int returned);
    }
}
