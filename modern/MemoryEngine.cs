#if !HELPER
#nullable disable // Shared C# 6 sources target the Windows .NET Framework helper.
#endif
// Native cleanup operations are derived from Henry++'s Mem Reduct (GPL-3.0).
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MemReduct.Fluent
{

[Flags]
public enum CleanAreas
{
    None = 0, WorkingSets = 1, SystemCache = 2, LowPriorityStandby = 4,
    ModifiedFileCache = 8, ModifiedPages = 16, Standby = 32, RegistryCache = 64, CombinePages = 128
}

#if !HELPER
public sealed record MemorySnapshot(ulong PhysicalTotal, ulong PhysicalAvailable, ulong CommitUsed,
    ulong CommitLimit, ulong? CacheCurrent, ulong? CachePeak)
{
    public double PhysicalPercent => Percent(PhysicalTotal - Math.Min(PhysicalAvailable, PhysicalTotal), PhysicalTotal);
    public double CommitPercent => Percent(CommitUsed, CommitLimit);
    public static double Percent(ulong used, ulong total) => total == 0 ? 0 : Math.Clamp(used * 100.0 / total, 0, 100);
    public static string Bytes(ulong bytes) => bytes >= 1024UL * 1024 * 1024
        ? (bytes / (1024.0 * 1024 * 1024)).ToString("0.0", CultureInfo.InvariantCulture) + " GB"
        : (bytes / (1024.0 * 1024)).ToString("0", CultureInfo.InvariantCulture) + " MB";
}

public sealed record CleanupResult(ulong FreedBytes, IReadOnlyList<string> Errors, CleanAreas CompletedAreas);
#else
public sealed class CleanupResult
{
    public ulong FreedBytes { get; private set; }
    public IReadOnlyList<string> Errors { get; private set; }
    public CleanAreas CompletedAreas { get; private set; }
    public CleanupResult(ulong bytes, IReadOnlyList<string> errors, CleanAreas completed)
    { FreedBytes = bytes; Errors = errors; CompletedAreas = completed; }
}
#endif

internal static class MemoryEngine
{
    internal const CleanAreas ManualOnlyAreas = CleanAreas.ModifiedPages | CleanAreas.Standby;
    private const CleanAreas AllAreas = (CleanAreas)255;
    internal static CleanAreas ForAutomaticCleanup(CleanAreas areas) => areas & ~ManualOnlyAreas;

#if !HELPER
    internal static (CleanAreas Area, string Key)[] GetOperations(CleanAreas areas, bool automatic)
    {
        ValidateAreas(areas);
        if (automatic) areas = ForAutomaticCleanup(areas);
        (CleanAreas Area, string Key)[] operations =
        [
            (CleanAreas.WorkingSets, "WorkingSetCheck.Content"), (CleanAreas.SystemCache, "SystemCacheCheck.Content"),
            (CleanAreas.ModifiedFileCache, "ModifiedFileCheck.Content"), (CleanAreas.ModifiedPages, "ModifiedPagesCheck.Content"),
            (CleanAreas.Standby, "FullStandbyCheck.Content"), (CleanAreas.LowPriorityStandby, "StandbyCheck.Content"),
            (CleanAreas.RegistryCache, "RegistryCheck.Content"), (CleanAreas.CombinePages, "CombineCheck.Content")
        ];
        return operations.Where(operation => areas.HasFlag(operation.Area)).ToArray();
    }
    public static MemorySnapshot Read()
    {
        var memory = ReadPhysical();
        var performance = new PerformanceInfo { Size = (uint)Marshal.SizeOf<PerformanceInfo>() };
        if (!GetPerformanceInfo(out performance, performance.Size)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var cache = new FileCacheInfo();
        var status = NtQuerySystemInformation(21, ref cache, Marshal.SizeOf<FileCacheInfo>(), out _);
        return new(memory.TotalPhysical, memory.AvailablePhysical,
            performance.CommitTotal.ToUInt64() * performance.PageSize.ToUInt64(),
            performance.CommitLimit.ToUInt64() * performance.PageSize.ToUInt64(),
            status >= 0 ? cache.Current.ToUInt64() : null, status >= 0 ? cache.Peak.ToUInt64() : null);
    }

    public static CleanupResult Clean(CleanAreas areas, bool automatic = false)
    {
        var operations = GetOperations(areas, automatic);
        if (operations.Length == 0) return new(0, Array.Empty<string>(), CleanAreas.None);
        if (!IsAdministrator) throw new UnauthorizedAccessException(UiText.Get("PermissionRequired"));
        var before = ReadPhysical();
        var (completed, errors) = RunOperations(operations, CleanOperation);
        var after = ReadPhysical();
        // RAM changes concurrently; only report an observed increase in available memory.
        return new(after.AvailablePhysical > before.AvailablePhysical ? after.AvailablePhysical - before.AvailablePhysical : 0, errors, completed);
    }

    internal static (CleanAreas Completed, List<string> Errors) RunOperations(
        (CleanAreas Area, string Key)[] operations, Action<CleanAreas, string, List<string>> execute)
    {
        var completed = CleanAreas.None;
        var errors = new List<string>();
        foreach (var (area, key) in operations)
        {
            var name = UiText.Get(key);
            var previousErrors = errors.Count;
            try
            {
                execute(area, name, errors);
                if (errors.Count == previousErrors) completed |= area;
            }
            catch (Win32Exception error) { errors.Add(name + ": " + error.Message); }
        }
        return (completed, errors);
    }
#else
    public static CleanupResult Clean(CleanAreas areas, bool automatic = false)
    {
        ValidateAreas(areas);
        if (automatic) areas = ForAutomaticCleanup(areas);
        if (!IsAdministrator) throw new UnauthorizedAccessException(UiText.Get("PermissionRequired"));
        var before = ReadPhysical();
        var errors = new List<string>();
        var completed = CleanAreas.None;
        var order = new[] { 1, 2, 8, 16, 32, 4, 64, 128 };
        foreach (var value in order)
        {
            var area = (CleanAreas)value;
            if ((areas & area) == 0) continue;
            var name = UiText.OperationName(area);
            var count = errors.Count;
            try { CleanOperation(area, name, errors); if (errors.Count == count) completed |= area; }
            catch (Win32Exception error) { errors.Add(name + ": " + error.Message); }
        }
        var after = ReadPhysical();
        return new CleanupResult(after.AvailablePhysical > before.AvailablePhysical ? after.AvailablePhysical - before.AvailablePhysical : 0, errors, completed);
    }
#endif
    public static bool IsAdministrator { get; } = ReadAdministrator();
    private static bool ReadAdministrator()
    { using (var identity = WindowsIdentity.GetCurrent()) return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); }

    private static void CleanOperation(CleanAreas area, string name, List<string> errors)
    {
        // Each operation owns its privilege scope, so a denied privilege cannot prevent unrelated cleanup.
        using (var profile = (area & (CleanAreas.WorkingSets | CleanAreas.LowPriorityStandby | ManualOnlyAreas | CleanAreas.CombinePages)) != 0
            ? new PrivilegeScope("SeProfileSingleProcessPrivilege") : null)
        using (var quota = area == CleanAreas.SystemCache ? new PrivilegeScope("SeIncreaseQuotaPrivilege") : null)
        {
        switch (area)
        {
            case CleanAreas.WorkingSets: RunListCommand(2, name, errors); break;
            case CleanAreas.SystemCache:
                if (!SetSystemFileCacheSize(new UIntPtr(ulong.MaxValue), new UIntPtr(ulong.MaxValue), 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
                break;
            case CleanAreas.ModifiedFileCache: FlushVolumeCaches(name, errors); break;
            case CleanAreas.ModifiedPages: RunListCommand(3, name, errors); break;
            case CleanAreas.Standby: RunListCommand(4, name, errors); break;
            case CleanAreas.LowPriorityStandby: RunListCommand(5, name, errors); break;
            case CleanAreas.RegistryCache: CheckStatus(NtSetSystemInformation(155, IntPtr.Zero, 0)); break;
            case CleanAreas.CombinePages:
                var combine = new CombineInfo();
                CheckStatus(NtSetSystemInformation(130, ref combine, Marshal.SizeOf<CombineInfo>()));
                break;
        }
        }
    }

    internal static void ValidateAreas(CleanAreas areas)
    {
        if (areas == CleanAreas.None || (areas & ~AllAreas) != 0)
            throw new ArgumentOutOfRangeException(nameof(areas), UiText.Get("InvalidAreas"));
    }

    private static MemoryStatus ReadPhysical()
    {
        var result = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref result)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return result;
    }

    private static void RunListCommand(int command, string name, List<string> errors)
    {
        var status = NtSetSystemInformation(80, ref command, sizeof(int));
        if (status < 0) errors.Add(name + "：" + new Win32Exception((int)RtlNtStatusToDosError(status)).Message);
    }

    private static void CheckStatus(int status)
    { if (status < 0) throw new Win32Exception((int)RtlNtStatusToDosError(status)); }

    internal static string VolumeDevicePath(string volume)
    {
        Guid parsed;
        if (volume.Length != 49 || !volume.StartsWith(@"\\?\Volume{", StringComparison.Ordinal)
            || !volume.EndsWith(@"}\", StringComparison.Ordinal) || !Guid.TryParse(volume.Substring(11, 36), out parsed))
            throw new ArgumentException("Invalid volume GUID path.", nameof(volume));
        return volume.Substring(0, volume.Length - 1);
    }

    private static void FlushVolumeCaches(string name, List<string> errors)
    {
        var buffer = new StringBuilder(1024);
        var search = FindFirstVolume(buffer, buffer.Capacity);
        if (search == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            while (true)
            {
                var volume = buffer.ToString();
                // Flush existing volume caches only; never lock, dismount or write raw disk data.
                using (var handle = CreateFile(VolumeDevicePath(volume), 0x40000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
                {
                    if (handle.IsInvalid || !FlushFileBuffers(handle))
                        errors.Add(name + " (" + volume + "): " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
                }
                if (FindNextVolume(search, buffer, buffer.Capacity)) continue;
                var error = Marshal.GetLastWin32Error();
                if (error != 18) throw new Win32Exception(error); // ERROR_NO_MORE_FILES
                break;
            }
        }
        finally { FindVolumeClose(search); }
    }

    private sealed class PrivilegeScope : IDisposable
    {
        private readonly SafeAccessTokenHandle _token;
        private TokenPrivileges _previous;
        public PrivilegeScope(string name)
        {
            if (!OpenProcessToken(new IntPtr(-1), 0x28, out _token)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                Luid luid;
                int returned;
                if (!LookupPrivilegeValue(null, name, out luid)) throw new Win32Exception(Marshal.GetLastWin32Error());
                var requested = new TokenPrivileges { Count = 1, Luid = luid, Attributes = 2 };
                if (!AdjustTokenPrivileges(_token, false, ref requested, Marshal.SizeOf<TokenPrivileges>(), out _previous, out returned))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (Marshal.GetLastWin32Error() != 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            catch { _token.Dispose(); throw; }
        }
        public void Dispose()
        {
            TokenPrivileges ignored;
            int returned;
            if (_previous.Count != 0)
                AdjustTokenPrivileges(_token, false, ref _previous, Marshal.SizeOf<TokenPrivileges>(), out ignored, out returned);
            _token.Dispose();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryStatus
    {
        public uint Length, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
            TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct PerformanceInfo
    {
        public uint Size;
        public UIntPtr CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable,
            SystemCache, KernelTotal, KernelPaged, KernelNonPaged, PageSize;
        public uint HandleCount, ProcessCount, ThreadCount;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct FileCacheInfo
    {
        public UIntPtr Current, Peak;
        public uint PageFaults;
        public UIntPtr Minimum, Maximum, CurrentWithTransition, PeakWithTransition;
        public uint TransitionRepurposeCount, Flags;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct CombineInfo { public IntPtr Handle; public UIntPtr PagesCombined; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TokenPrivileges { public uint Count; public Luid Luid; public uint Attributes; }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetPerformanceInfo(out PerformanceInfo info, uint size);
    [DllImport("ntdll.dll")] private static extern int NtQuerySystemInformation(int type, ref FileCacheInfo info, int size, out int returned);
    [DllImport("ntdll.dll")] private static extern int NtSetSystemInformation(int type, ref int value, int size);
    [DllImport("ntdll.dll")] private static extern int NtSetSystemInformation(int type, IntPtr value, int size);
    [DllImport("ntdll.dll")] private static extern int NtSetSystemInformation(int type, ref CombineInfo value, int size);
    [DllImport("ntdll.dll")] private static extern uint RtlNtStatusToDosError(int status);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetSystemFileCacheSize(UIntPtr minimum, UIntPtr maximum, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindFirstVolumeW", SetLastError = true)]
    private static extern IntPtr FindFirstVolume(StringBuilder name, int length);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindNextVolumeW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FindNextVolume(IntPtr search, StringBuilder name, int length);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FindVolumeClose(IntPtr search);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateFileW", SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FlushFileBuffers(SafeFileHandle handle);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool LookupPrivilegeValue(string system, string name, out Luid luid);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AdjustTokenPrivileges(SafeAccessTokenHandle token, bool disableAll,
        ref TokenPrivileges requested, int size, out TokenPrivileges previous, out int returned);
}
}
