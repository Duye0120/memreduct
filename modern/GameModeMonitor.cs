using System.Runtime.InteropServices;
using System.Text;

namespace MemReduct.Fluent;

internal static class GameModeMonitor
{
    internal static bool IsFullscreenApp()
    {
        // Exclusive Direct3D can bypass normal window geometry.
        if (SHQueryUserNotificationState(out var state) >= 0 && state == 3) return true;
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero || window == GetShellWindow() || window == GetDesktopWindow() || IsIconic(window)) return false;
        GetWindowThreadProcessId(window, out var processId);
        if (processId == Environment.ProcessId) return false;
        var name = new StringBuilder(256);
        if (GetClassName(window, name, name.Capacity) == 0 || IsShellClass(name.ToString())) return false;
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(window, 2), ref monitor)) return false;
        if (DwmGetWindowAttribute(window, 9, out var bounds, Marshal.SizeOf<Rect>()) < 0
            && !GetWindowRect(window, out bounds)) return false;
        // shortcut: all foreground full-screen apps are protected, including video; use a game allowlist only if requested.
        return CoversMonitor(bounds, monitor.Monitor);
    }

    internal static bool IsShellClass(string name) => name is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";

    internal static bool CoversMonitor(Rect window, Rect monitor) => monitor.Right > monitor.Left && monitor.Bottom > monitor.Top
        && window.Right > window.Left && window.Bottom > window.Top
        && window.Left <= monitor.Left + 2 && window.Top <= monitor.Top + 2
        && window.Right >= monitor.Right - 2 && window.Bottom >= monitor.Bottom - 2;

    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int size);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect value, int size);
}
