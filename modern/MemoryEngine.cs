// Native cleanup operations are derived from Henry++'s Mem Reduct (GPL-3.0).
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace MemReduct.Fluent;

[Flags]
public enum CleanAreas { None = 0, WorkingSets = 1, SystemCache = 2, LowPriorityStandby = 4 }

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

public sealed record CleanupResult(ulong FreedBytes, IReadOnlyList<string> Errors);

internal static class MemoryEngine
{
    public static bool IsAdministrator { get; } = ReadAdministrator();
    private static bool ReadAdministrator()
    { using var identity = WindowsIdentity.GetCurrent(); return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); }

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

    public static CleanupResult Clean(CleanAreas areas)
    {
        ValidateAreas(areas);
        if (!IsAdministrator) throw new UnauthorizedAccessException(UiText.Get("PermissionRequired"));
        var errors = new List<string>();
        var before = ReadPhysical();
        using var profile = (areas & (CleanAreas.WorkingSets | CleanAreas.LowPriorityStandby)) != 0
            ? new PrivilegeScope("SeProfileSingleProcessPrivilege") : null;
        using var quota = areas.HasFlag(CleanAreas.SystemCache) ? new PrivilegeScope("SeIncreaseQuotaPrivilege") : null;

        if (areas.HasFlag(CleanAreas.WorkingSets)) RunListCommand(2, UiText.Get("WorkingSetCheck.Content"), errors);
        if (areas.HasFlag(CleanAreas.SystemCache) && !SetSystemFileCacheSize(UIntPtr.MaxValue, UIntPtr.MaxValue, 0))
            errors.Add(UiText.Get("SystemCacheCheck.Content") + ": " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
        if (areas.HasFlag(CleanAreas.LowPriorityStandby)) RunListCommand(5, UiText.Get("StandbyCheck.Content"), errors);

        var after = ReadPhysical();
        // RAM changes concurrently; only report an observed increase in available memory.
        return new(after.AvailablePhysical > before.AvailablePhysical ? after.AvailablePhysical - before.AvailablePhysical : 0, errors);
    }

    internal static void ValidateAreas(CleanAreas areas)
    {
        const CleanAreas allowed = CleanAreas.WorkingSets | CleanAreas.SystemCache | CleanAreas.LowPriorityStandby;
        if (areas == CleanAreas.None || (areas & ~allowed) != 0)
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

    private sealed class PrivilegeScope : IDisposable
    {
        private readonly SafeAccessTokenHandle _token;
        private TokenPrivileges _previous;
        public PrivilegeScope(string name)
        {
            if (!OpenProcessToken(new IntPtr(-1), 0x28, out _token)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                if (!LookupPrivilegeValue(null, name, out var luid)) throw new Win32Exception(Marshal.GetLastWin32Error());
                var requested = new TokenPrivileges { Count = 1, Luid = luid, Attributes = 2 };
                if (!AdjustTokenPrivileges(_token, false, ref requested, Marshal.SizeOf<TokenPrivileges>(), out _previous, out _))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (Marshal.GetLastWin32Error() != 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            catch { _token.Dispose(); throw; }
        }
        public void Dispose()
        {
            if (_previous.Count != 0)
                AdjustTokenPrivileges(_token, false, ref _previous, Marshal.SizeOf<TokenPrivileges>(), out _, out _);
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
    private struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TokenPrivileges { public uint Count; public Luid Luid; public uint Attributes; }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetPerformanceInfo(out PerformanceInfo info, uint size);
    [DllImport("ntdll.dll")] private static extern int NtQuerySystemInformation(int type, ref FileCacheInfo info, int size, out int returned);
    [DllImport("ntdll.dll")] private static extern int NtSetSystemInformation(int type, ref int value, int size);
    [DllImport("ntdll.dll")] private static extern uint RtlNtStatusToDosError(int status);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetSystemFileCacheSize(UIntPtr minimum, UIntPtr maximum, uint flags);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool LookupPrivilegeValue(string? system, string name, out Luid luid);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AdjustTokenPrivileges(SafeAccessTokenHandle token, bool disableAll,
        ref TokenPrivileges requested, int size, out TokenPrivileges previous, out int returned);
}
