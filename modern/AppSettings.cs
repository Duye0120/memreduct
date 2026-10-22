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

    public bool IsAutoDue(double physicalPercent, DateTimeOffset now, DateTimeOffset lastAttempt, bool gameActive = false)
    {
        if ((GameMode && gameActive) || AutomaticAreas == CleanAreas.None || now - lastAttempt < TimeSpan.FromSeconds(30)) return false;
        return (AutoThreshold && physicalPercent >= Threshold) ||
            (AutoInterval && now - lastAttempt >= TimeSpan.FromMinutes(IntervalMinutes));
    }
}

internal static class SettingsStore
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        if (!File.Exists(FilePath)) return new();
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath))
            ?? throw new InvalidDataException(UiText.Get("EmptySettings"));
        settings.Normalize();
        return settings;
    }

    public static void Save(AppSettings settings)
    {
        settings.Normalize();
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, overwrite: true);
    }
}
