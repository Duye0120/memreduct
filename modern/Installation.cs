using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace MemReduct.Fluent;

internal static class Installation
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{5B9D5C89-F01C-4AB6-A593-DA69B3835832}_is1";
    internal static string DirectoryPath { get; } = ReadDirectory();
    private static string ReadDirectory()
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = machine.OpenSubKey(UninstallKey);
        return ResolveDirectory(key?.GetValue("InstallLocation") as string);
    }

    internal static string ResolveDirectory(string? registeredDirectory)
    {
        if (string.IsNullOrWhiteSpace(registeredDirectory))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MemReduct Fluent");
        if (!Path.IsPathFullyQualified(registeredDirectory) || registeredDirectory.StartsWith(@"\\"))
            throw new InvalidDataException("Invalid registered installation directory.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(registeredDirectory));
    }
    internal static string ExecutablePath => Path.Combine(DirectoryPath, "MemReduct.Fluent.exe");
    internal static bool IsInstalled => IsInstalledPath(Environment.ProcessPath!);
    internal static bool IsInstalledPath(string path) => MatchesExecutable(path, DirectoryPath);
    internal static bool MatchesExecutable(string path, string directory) =>
        string.Equals(Path.GetFullPath(path), Path.Combine(directory, "MemReduct.Fluent.exe"), StringComparison.OrdinalIgnoreCase);
    internal static string UserDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MemReductFluent");
    internal static string SettingsPath => Path.Combine(IsInstalled ? UserDirectory : AppContext.BaseDirectory, "settings.json");

    // A persistent elevated task must never execute a user-writable portable build or load writable DLLs.
    internal static void RequireProtectedInstall()
    {
        if (!IsInstalled) throw new InvalidOperationException(UiText.Get("InstalledOnly"));
        // A writable ancestor can allow replacing the entire installation directory.
        for (var parent = Directory.GetParent(DirectoryPath); parent is not null; parent = parent.Parent)
            RequireProtectedPath(parent, ancestor: true);
        RequireProtectedPath(new DirectoryInfo(DirectoryPath));
        foreach (var entry in new DirectoryInfo(DirectoryPath).EnumerateFileSystemInfos()) CheckTree(entry);
    }

    private static void CheckTree(FileSystemInfo entry)
    {
        RequireProtectedPath(entry);
        if (entry is DirectoryInfo directory)
            foreach (var child in directory.EnumerateFileSystemInfos()) CheckTree(child);
    }

    internal static void RequireProtectedPath(FileSystemInfo entry, bool ancestor = false)
    {
        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new UnauthorizedAccessException("Installation contains a symbolic link: " + entry.FullName);
        FileSystemSecurity acl = entry is DirectoryInfo directory ? directory.GetAccessControl() : ((FileInfo)entry).GetAccessControl();
        if (!IsTrustedOwner(acl.GetOwner(typeof(SecurityIdentifier))?.Value ?? ""))
            throw new UnauthorizedAccessException("Installation owner is not protected: " + entry.FullName);
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && (rule.PropagationFlags & PropagationFlags.InheritOnly) == 0
                && (ancestor ? GrantsUntrustedReplacement(rule.IdentityReference.Value, rule.FileSystemRights)
                    : GrantsUntrustedWrite(rule.IdentityReference.Value, rule.FileSystemRights)))
                throw new UnauthorizedAccessException("Installation is writable by an unprivileged account: " + entry.FullName);
    }

    internal static bool IsTrustedOwner(string sid) => sid is "S-1-5-18" or "S-1-5-32-544"
        or "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";
    internal static bool GrantsUntrustedReplacement(string sid, FileSystemRights rights) => !IsTrustedOwner(sid)
        && (rights & (FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions
            | FileSystemRights.TakeOwnership | FileSystemRights.WriteAttributes | FileSystemRights.WriteExtendedAttributes)) != 0;
    internal static bool GrantsUntrustedWrite(string sid, FileSystemRights rights) => !IsTrustedOwner(sid)
        && (rights & (FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles
            | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership)) != 0;
}
