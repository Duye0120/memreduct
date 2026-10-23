using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace MemReduct.Fluent;

internal static class UserDataAccess
{
    internal static void Run(Action action) => Run(() => { action(); return 0; });

    internal static T Run<T>(Func<T> action)
    {
        using var current = WindowsIdentity.GetCurrent();
        using var token = GetLimitedToken(current);
        return WindowsIdentity.RunImpersonated(token ?? current.AccessToken, action);
    }

    internal static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        using var current = WindowsIdentity.GetCurrent();
        using var token = GetLimitedToken(current);
        return await WindowsIdentity.RunImpersonatedAsync(token ?? current.AccessToken, action);
    }

    private static SafeAccessTokenHandle? GetLimitedToken(WindowsIdentity current)
    {
        if (!new WindowsPrincipal(current).IsInRole(WindowsBuiltInRole.Administrator)) return null;
        // Linked tokens can be identification-only; use this desktop's same-account ordinary shell token for I/O.
        var shell = GetShellWindow();
        if (shell == IntPtr.Zero || GetWindowThreadProcessId(shell, out var processId) == 0 || processId == 0)
            throw new UnauthorizedAccessException(UiText.Get("UserAccessUnavailable"));
        using var process = OpenProcess(0x1000, false, processId); // PROCESS_QUERY_LIMITED_INFORMATION
        if (process.IsInvalid || !OpenProcessToken(process, 0x0a, out var token)) // TOKEN_QUERY | TOKEN_DUPLICATE
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            throw new UnauthorizedAccessException(UiText.Get("UserAccessUnavailable"), error);
        }
        try
        {
            if (token.IsInvalid) throw new UnauthorizedAccessException(UiText.Get("UserAccessUnavailable"));
            using var limited = new WindowsIdentity(token.DangerousGetHandle());
            if (limited.User?.Value != current.User?.Value || new WindowsPrincipal(limited).IsInRole(WindowsBuiltInRole.Administrator)
                || limited.ImpersonationLevel is TokenImpersonationLevel.Anonymous or TokenImpersonationLevel.Identification)
                throw new UnauthorizedAccessException(UiText.Get("UserAccessUnavailable"));
            return token;
        }
        catch { token.Dispose(); throw; }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
