using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.UI.ViewManagement;

namespace MemReduct.Fluent;

internal enum CleanupFeedback { None, Cleaning, Success, Warning, Failed }

internal sealed class TrayIcon : IDisposable
{
    private const uint CallbackMessage = 0x8001;
    internal static readonly uint ShowMessage = RegisterWindowMessage("MemReduct.Fluent.Show.50d9cf93");
    private readonly IntPtr _window;
    private readonly SubclassProc _procedure;
    private readonly Action _show, _exit, _clean, _toggleGameMode, _toggleConfirmation;
    private readonly Func<bool> _gameModeEnabled, _confirmationRequired;
    private readonly uint _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private NotifyIconData _data;
    private bool _added, _version4;
    private long _lastActivation = long.MinValue / 2;
    private string _tooltip = "";
    private (int Percent, int Size, uint Color, int Frame) _glyph = (-1, 0, 0, -1);
    private readonly AccessibilitySettings _accessibility = new();
    private readonly UISettings _uiSettings = new();
    public bool IsVisible => _added;

    public TrayIcon(IntPtr window, Action show, Action exit, Action clean, Action toggleGameMode, Func<bool> gameModeEnabled,
        Action toggleConfirmation, Func<bool> confirmationRequired)
    {
        _window = window; _show = show; _exit = exit; _clean = clean;
        _toggleGameMode = toggleGameMode; _gameModeEnabled = gameModeEnabled;
        _toggleConfirmation = toggleConfirmation; _confirmationRequired = confirmationRequired;
        _procedure = WindowProc;
        if (!SetWindowSubclass(window, _procedure, 1, UIntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var taskbar = FindWindow("Shell_TrayWnd", null);
        var smallIcon = Math.Clamp(GetSystemMetricsForDpi(49, GetDpiForWindow(taskbar != IntPtr.Zero ? taskbar : window)), 16, 256);
        _data = new NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(), Window = window, Id = 1,
            Flags = 1 | 2 | 4 | 0x80, Callback = CallbackMessage,
            Icon = LoadImage(IntPtr.Zero, Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"), 1, smallIcon, smallIcon, 0x10),
            Tip = "Mem Reduct Fluent", Info = "", InfoTitle = ""
        };
        if (_data.Icon == IntPtr.Zero) { RemoveWindowSubclass(window, _procedure, 1); throw new Win32Exception(Marshal.GetLastWin32Error()); }
        // Allow only the harmless show-window message from another unelevated instance.
        ChangeWindowMessageFilterEx(window, ShowMessage, 1, IntPtr.Zero);
        Add();
    }

    private void Add()
    {
        _data.Flags = 1 | 2 | 4 | 0x80;
        _added = Shell_NotifyIcon(0, ref _data);
        if (_added) { _data.TimeoutOrVersion = 4; _version4 = Shell_NotifyIcon(4, ref _data); }
    }

    internal static bool IsActivation(uint action, bool version4) => version4 ? action is 0x400 or 0x401 : action is 0x202 or 0x401;
    internal static bool IsContextMenu(uint action, bool version4) => version4 ? action == 0x7b : action is 0x205 or 0x7b;

    internal static uint StatusColor(double percent, CleanupFeedback feedback, bool gameActive, bool dark) => feedback switch
    {
        CleanupFeedback.Cleaning => dark ? 0xc4b5fdu : 0x7c3aedu,
        CleanupFeedback.Success => dark ? 0x4ade80u : 0x15803du,
        CleanupFeedback.Warning => dark ? 0xfbbf24u : 0xb45309u,
        CleanupFeedback.Failed => dark ? 0xf87171u : 0xdc2626u,
        _ when percent >= 90 => dark ? 0xf87171u : 0xdc2626u,
        _ when percent >= 70 => dark ? 0xfbbf24u : 0xb45309u,
        _ when gameActive => dark ? 0xc4b5fdu : 0x7c3aedu,
        _ => dark ? 0x4ade80u : 0x15803du
    };

    public void Update(double percent, CleanupFeedback feedback, string status, bool gameActive, int frame = -1)
    {
        var value = UiText.Get("TrayUsage", percent) + "\n" + status + "\n" + UiText.Get(_confirmationRequired() ? "TrayClickConfirmHint" : "TrayClickDirectHint");
        if (value.Length > 127) value = value[..127];
        var taskbar = FindWindow("Shell_TrayWnd", null);
        var size = GetSystemMetricsForDpi(49, GetDpiForWindow(taskbar != IntPtr.Zero ? taskbar : _window));
        var foreground = _uiSettings.GetColorValue(UIColorType.Foreground);
        var color = _accessibility.HighContrast ? (uint)(foreground.R << 16 | foreground.G << 8 | foreground.B)
            : StatusColor(percent, feedback, gameActive,
                Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) is not int light || light == 0);
        var glyph = ((int)Math.Clamp(Math.Round(percent), 0, 100), Math.Clamp(size, 16, 256), color, frame);
        var redraw = glyph != _glyph;
        if (value == _tooltip && !redraw && _added) return;
        var previousIcon = _data.Icon;
        if (redraw) _data.Icon = TrayGlyph.Create(glyph.Item1, glyph.Item2, color, out _, frame);
        _tooltip = value;
        _data.Tip = value;
        _data.Flags = 4 | 0x80 | (redraw ? 2u : 0u);
        if (_added) _added = Shell_NotifyIcon(1, ref _data);
        if (!_added) Add();
        if (redraw) { _glyph = glyph; DestroyIcon(previousIcon); }
    }

    public void NotifySuccess(string freed)
    {
        if (!_added) return;
        var notification = _data;
        notification.Flags = 0x10 | 0x40; // NIF_INFO | NIF_REALTIME: never queue a stale success balloon.
        notification.InfoTitle = UiText.Get("CleanupSucceeded");
        notification.Info = UiText.Get("CleanupNotification", freed);
        notification.InfoFlags = 1 | 0x10 | 0x80; // Information, silent, respect quiet time.
        Shell_NotifyIcon(1, ref notification);
    }

    private IntPtr WindowProc(IntPtr window, uint message, UIntPtr wparam, IntPtr lparam, UIntPtr id, UIntPtr reference)
    {
        if (message == ShowMessage) { _show(); return IntPtr.Zero; }
        if (message == _taskbarCreated) { Add(); return IntPtr.Zero; }
        if (message == CallbackMessage)
        {
            var action = (uint)lparam.ToInt64() & 0xffff;
            if (IsActivation(action, _version4) && Environment.TickCount64 - _lastActivation >= 750)
            { _lastActivation = Environment.TickCount64; _clean(); }
            if (IsContextMenu(action, _version4))
            {
                var menu = CreatePopupMenu();
                if (menu != IntPtr.Zero)
                {
                    try
                    {
                        // A separate zero-size tool window owns the menu; never activate the main window on right-click.
                        using var owner = new MenuOwner();
                        if (owner.Handle == IntPtr.Zero) return IntPtr.Zero;
                        AppendMenu(menu, 0, 1, UiText.Get("TrayOpen"));
                        AppendMenu(menu, _gameModeEnabled() ? 8u : 0u, 3, UiText.Get("GameModeMenu"));
                        AppendMenu(menu, _confirmationRequired() ? 8u : 0u, 4, UiText.Get("ConfirmLabel.Text"));
                        AppendMenu(menu, 0, 2, UiText.Get("TrayExit"));
                        GetCursorPos(out var point);
                        owner.Activate();
                        var selected = TrackPopupMenuEx(menu, 0x100 | 0x80 | 2, point.X, point.Y, owner.Handle, IntPtr.Zero);
                        PostMessage(owner.Handle, 0, UIntPtr.Zero, IntPtr.Zero);
                        if (selected == 1) _show();
                        if (selected == 2) _exit();
                        if (selected == 3) _toggleGameMode();
                        if (selected == 4) _toggleConfirmation();
                    }
                    finally { DestroyMenu(menu); }
                }
            }
            return IntPtr.Zero;
        }
        return DefSubclassProc(window, message, wparam, lparam);
    }

    internal sealed class MenuOwner : IDisposable
    {
        internal IntPtr Handle { get; } = CreateWindowEx(0x80, "STATIC", "", 0x80000000,
            0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        internal void Activate() { ShowWindow(Handle, 4); SetForegroundWindow(Handle); }
        public void Dispose() { if (Handle != IntPtr.Zero) DestroyWindow(Handle); }
    }

    public void Dispose()
    {
        if (_added) Shell_NotifyIcon(2, ref _data);
        RemoveWindowSubclass(_window, _procedure, 1);
        if (_data.Icon != IntPtr.Zero) DestroyIcon(_data.Icon);
        _data.Icon = IntPtr.Zero;
        _added = false;
    }

    public static void ShowExistingWindow()
    {
        var window = FindWindow(null, "Mem Reduct Fluent");
        if (window != IntPtr.Zero) PostMessage(window, ShowMessage, UIntPtr.Zero, IntPtr.Zero);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size; public IntPtr Window; public uint Id, Flags, Callback; public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    private delegate IntPtr SubclassProc(IntPtr hwnd, uint msg, UIntPtr wparam, IntPtr lparam, UIntPtr id, UIntPtr reference);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string title,
        uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("comctl32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc proc, UIntPtr id, UIntPtr reference);
    [DllImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc proc, UIntPtr id);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint message, UIntPtr wparam, IntPtr lparam);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int cx, int cy, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr window, IntPtr parameters);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(IntPtr window, uint message, UIntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string? title);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int index, uint dpi);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ChangeWindowMessageFilterEx(IntPtr window, uint message, uint action, IntPtr filter);
}
