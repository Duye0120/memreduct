#if !HELPER
#nullable disable // Shared C# 6 sources target the Windows .NET Framework helper.
#endif
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MemReduct.Fluent
{

internal static class UserDataAccess
{
    // Setup may already be elevated; never reopen the writable UI with the setup token.
    internal static void StartInstalledUi()
    {
        var executable = HelperTasks.UiExecutable();
        using (var current = WindowsIdentity.GetCurrent())
        using (var limited = GetLimitedToken(current))
        {
            if (limited == null)
            {
                using (var process = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable) }))
                    if (process == null) throw new InvalidOperationException("Could not reopen the application.");
                return;
            }
            SafeAccessTokenHandle primary;
            if (!DuplicateTokenEx(limited, 0x0b, IntPtr.Zero, 2, 1, out primary)) throw new Win32Exception(Marshal.GetLastWin32Error());
            using (primary)
            {
                var startup = new StartupInfo { Size = Marshal.SizeOf(typeof(StartupInfo)), Desktop = @"winsta0\default" };
                ProcessInformation process;
                if (!CreateProcessWithTokenW(primary, 1, executable, new StringBuilder("\"" + executable + "\""), 0,
                    IntPtr.Zero, Path.GetDirectoryName(executable), ref startup, out process))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                using (var processHandle = new SafeProcessHandle(process.Process, true))
                using (var threadHandle = new SafeWaitHandle(process.Thread, true)) { }
            }
        }
    }

    internal static void Run(Action action) => Run(() => { action(); return 0; });

    internal static T Run<T>(Func<T> action)
    {
        using (var current = WindowsIdentity.GetCurrent())
        using (var token = GetLimitedToken(current))
            return WindowsIdentity.RunImpersonated(token ?? current.AccessToken, action);
    }

#if !HELPER
    internal static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        using var current = WindowsIdentity.GetCurrent();
        using var token = GetLimitedToken(current);
        return await WindowsIdentity.RunImpersonatedAsync(token ?? current.AccessToken, action);
    }
#endif

    private static SafeAccessTokenHandle GetLimitedToken(WindowsIdentity current)
    {
        if (!new WindowsPrincipal(current).IsInRole(WindowsBuiltInRole.Administrator)) return null;
        // Linked tokens can be identification-only; use this desktop's same-account ordinary shell token for I/O.
        var shell = GetShellWindow();
        uint processId;
        if (shell == IntPtr.Zero || GetWindowThreadProcessId(shell, out processId) == 0 || processId == 0)
            throw new UnauthorizedAccessException(UiText.Get("UserAccessUnavailable"));
        using (var process = OpenProcess(0x1000, false, processId)) // PROCESS_QUERY_LIMITED_INFORMATION
        {
            SafeAccessTokenHandle token;
            if (process.IsInvalid || !OpenProcessToken(process, 0x0a, out token)) // TOKEN_QUERY | TOKEN_DUPLICATE
            {
                var error = new Win32Exception(Marshal.GetLastWin32Error());
                throw new UnauthorizedAccessException(UiText.Get("UserAccessUnavailable"), error);
            }
            try
            {
                if (token.IsInvalid) throw new UnauthorizedAccessException(UiText.Get("UserAccessUnavailable"));
                using (var limited = new WindowsIdentity(token.DangerousGetHandle()))
                    if (limited.User?.Value != current.User?.Value || new WindowsPrincipal(limited).IsInRole(WindowsBuiltInRole.Administrator)
                        || limited.ImpersonationLevel == TokenImpersonationLevel.Anonymous || limited.ImpersonationLevel == TokenImpersonationLevel.Identification)
                        throw new UnauthorizedAccessException(UiText.Get("UserAccessUnavailable"));
                return token;
            }
            catch { token.Dispose(); throw; }
        }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(SafeAccessTokenHandle token, uint access, IntPtr attributes, int level, int type, out SafeAccessTokenHandle duplicate);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern bool CreateProcessWithTokenW(SafeAccessTokenHandle token, uint logon, string application, StringBuilder command,
        uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInformation process);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        internal int Size;
        internal string Reserved, Desktop, Title;
        internal int X, Y, Width, Height, XChars, YChars, Fill, Flags;
        internal short Show, ReservedSize;
        internal IntPtr ReservedData, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation { internal IntPtr Process, Thread; internal int ProcessId, ThreadId; }
}
}
