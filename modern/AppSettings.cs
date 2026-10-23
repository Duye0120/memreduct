using System.IO;
using System.Text.Json;

namespace MemReduct.Fluent;

public sealed class AppSettings
{
    public string Theme { get; set; } = "System";
    public string Language { get; set; } = "System";
    public bool SidebarExpanded { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool ConfirmCleanup { get; set; } = true;
    public bool TrayCleanupHintShown { get; set; }
    public bool NotifyAfterCleanup { get; set; }
    public bool CleanWorkingSets { get; set; } = true;
    public bool CleanSystemCache { get; set; } = true;
    public bool CleanLowPriorityStandby { get; set; } = true;
    public bool CleanModifiedFileCache { get; set; }
    public bool CleanModifiedPages { get; set; }
    public bool CleanStandby { get; set; }
    public bool CleanRegistryCache { get; set; }
    public bool CombineMemoryPages { get; set; }
    public bool AutoThreshold { get; set; }
    public bool GameMode { get; set; } = true;
    public int Threshold { get; set; } = 90;
    public bool AutoInterval { get; set; }
    public int IntervalMinutes { get; set; } = 30;
    public DateTimeOffset? LastCleanUtc { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public CleanAreas Areas =>
        (CleanWorkingSets ? CleanAreas.WorkingSets : 0) |
        (CleanSystemCache ? CleanAreas.SystemCache : 0) |
        (CleanLowPriorityStandby ? CleanAreas.LowPriorityStandby : 0) |
        (CleanModifiedFileCache ? CleanAreas.ModifiedFileCache : 0) |
        (CleanModifiedPages ? CleanAreas.ModifiedPages : 0) |
        (CleanStandby ? CleanAreas.Standby : 0) |
        (CleanRegistryCache ? CleanAreas.RegistryCache : 0) |
        (CombineMemoryPages ? CleanAreas.CombinePages : 0);

    [System.Text.Json.Serialization.JsonIgnore]
    public CleanAreas AutomaticAreas => MemoryEngine.ForAutomaticCleanup(Areas);

    public void Normalize()
    {
        Threshold = Math.Clamp(Threshold, 50, 99);
        IntervalMinutes = Math.Clamp(IntervalMinutes, 1, 1440);
        if (Theme is not ("System" or "Light" or "Dark")) Theme = "System";
        if (Language is not ("System" or "zh-CN" or "en-US")) Language = "System";
    }

    public bool IsAutoDue(double physicalPercent, DateTimeOffset now, DateTimeOffset lastAttempt, bool gameActive = false,
        DateTimeOffset thresholdRetryAfter = default)
    {
        if ((GameMode && gameActive) || AutomaticAreas == CleanAreas.None || now - lastAttempt < TimeSpan.FromSeconds(30)) return false;
        return (AutoThreshold && physicalPercent >= Threshold && now >= thresholdRetryAfter) ||
            (AutoInterval && now - lastAttempt >= TimeSpan.FromMinutes(IntervalMinutes));
    }
}

internal static class SettingsStore
{
    private static string FilePath => Installation.SettingsPath;

    internal static void ImportIfMissing(string source, string destination)
    {
        if (UserDataAccess.Run(() => File.Exists(destination))) return;
        var json = UserDataAccess.Run(() => ReadSettings(source));
        _ = JsonSerializer.Deserialize<AppSettings>(json) ?? throw new InvalidDataException(UiText.Get("EmptySettings"));
        // Preserve the source and never replace an existing installed profile, including on upgrade.
        UserDataAccess.Run(() => WriteSettings(destination, json, overwrite: false));
    }

    public static AppSettings Load() => Load(FilePath);

    internal static AppSettings Load(string path)
    {
        var json = UserDataAccess.Run(() => File.Exists(path) ? ReadSettings(path) : null);
        if (json is null) return new AppSettings();
        var settings = JsonSerializer.Deserialize<AppSettings>(json)
            ?? throw new InvalidDataException(UiText.Get("EmptySettings"));
        settings.Normalize();
        return settings;
    }

    private static string ReadSettings(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 65536) throw new InvalidDataException(UiText.Get("SettingsTooLarge"));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static void Save(AppSettings settings) => Save(settings, FilePath);

    internal static void Save(AppSettings settings, string path)
    {
        settings.Normalize();
        // Initialize JSON outside impersonation; runtime assemblies may not load under the limited token.
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        UserDataAccess.Run(() => WriteSettings(path, json, overwrite: true));
    }

    private static void WriteSettings(string path, string json, bool overwrite)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var created = false;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                using var writer = new StreamWriter(stream);
                writer.Write(json);
            }
            File.Move(temporary, path, overwrite);
        }
        finally { if (created && File.Exists(temporary)) File.Delete(temporary); }
    }
}
