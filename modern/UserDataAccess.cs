using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace MemReduct.Fluent;

internal static class UserDataAccess
{
    private const int TokenLinkedToken = 19;
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
        // User-writable settings/cache paths must not become elevated file-write primitives.
        if (!GetTokenInformation(current.AccessToken, TokenLinkedToken, out var linked, IntPtr.Size, out _))
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            throw new UnauthorizedAccessException(UiText.Get("UserAccessUnavailable"), error);
        }
        var token = new SafeAccessTokenHandle(linked);
        try
        {
            if (token.IsInvalid) throw new UnauthorizedAccessException(UiText.Get("UserAccessUnavailable"));
            using var limited = new WindowsIdentity(token.DangerousGetHandle());
            if (limited.User?.Value != current.User?.Value || new WindowsPrincipal(limited).IsInRole(WindowsBuiltInRole.Administrator))
                throw new UnauthorizedAccessException(UiText.Get("UserAccessUnavailable"));
            return token;
        }
        catch { token.Dispose(); throw; }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass,
        out IntPtr information, int length, out int returnedLength);
}
