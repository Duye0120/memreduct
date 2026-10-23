using Microsoft.Win32;

namespace MemReduct.Fluent;

internal static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MemReduct.Fluent";
    private static string Command => "\"" + Environment.ProcessPath + "\" --minimized";

    public static bool IsEnabled()
    {
        if (Installation.IsInstalled) return HelperTasks.StartupEnabled();
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
    }
    public static async Task SetEnabledAsync(bool enabled)
    {
        if (Installation.IsInstalled)
        {
            await HelperClient.ConfigureAsync(enabled ? "on" : "off");
            if (await Task.Run(IsEnabled) != enabled) throw new InvalidOperationException(UiText.Get("StartupFailed"));
            return;
        }
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        if (enabled) key.SetValue(ValueName, Command, RegistryValueKind.String);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
