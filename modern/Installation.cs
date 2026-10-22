using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace MemReduct.Fluent;

internal static class Installation
{
    internal static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MemReduct Fluent");
    internal static string ExecutablePath => Path.Combine(DirectoryPath, "MemReduct.Fluent.exe");
    internal static bool IsInstalled => IsInstalledPath(Environment.ProcessPath!);
    internal static bool IsInstalledPath(string path) => string.Equals(Path.GetFullPath(path), ExecutablePath, StringComparison.OrdinalIgnoreCase);
    internal static string UserDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MemReductFluent");
    internal static string SettingsPath => Path.Combine(IsInstalled ? UserDirectory : AppContext.BaseDirectory, "settings.json");

    // A persistent elevated task must never execute a user-writable portable build or load writable DLLs.
    internal static void RequireProtectedInstall()
    {
        if (!IsInstalled) throw new InvalidOperationException(UiText.Get("InstalledOnly"));
        RequireProtectedPath(new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)));
        RequireProtectedPath(new DirectoryInfo(DirectoryPath));
        foreach (var entry in new DirectoryInfo(DirectoryPath).EnumerateFileSystemInfos()) CheckTree(entry);
    }

    private static void CheckTree(FileSystemInfo entry)
    {
        RequireProtectedPath(entry);
        if (entry is DirectoryInfo directory)
            foreach (var child in directory.EnumerateFileSystemInfos()) CheckTree(child);
    }

    internal static void RequireProtectedPath(FileSystemInfo entry)
    {
        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new UnauthorizedAccessException("Installation contains a symbolic link: " + entry.FullName);
        FileSystemSecurity acl = entry is DirectoryInfo directory ? directory.GetAccessControl() : ((FileInfo)entry).GetAccessControl();
        if (!IsTrustedOwner(acl.GetOwner(typeof(SecurityIdentifier))?.Value ?? ""))
            throw new UnauthorizedAccessException("Installation owner is not protected: " + entry.FullName);
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && (rule.PropagationFlags & PropagationFlags.InheritOnly) == 0
                && GrantsUntrustedWrite(rule.IdentityReference.Value, rule.FileSystemRights))
                throw new UnauthorizedAccessException("Installation is writable by an unprivileged account: " + entry.FullName);
    }

    internal static bool IsTrustedOwner(string sid) => sid is "S-1-5-18" or "S-1-5-32-544"
        or "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";
    internal static bool GrantsUntrustedWrite(string sid, FileSystemRights rights) => !IsTrustedOwner(sid)
        && (rights & (FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles
            | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership)) != 0;
}
