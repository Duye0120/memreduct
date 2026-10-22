using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;

namespace MemReduct.Fluent;

public sealed record CleanupHistoryEntry(string TimeLabel, string Outcome, string Details);

public sealed partial class MainWindow
{
    private readonly ObservableCollection<CleanupHistoryEntry> _cleanupHistory = [];

    private void RecordCleanup(bool manual, CleanupResult? result, TimeSpan duration, string? failure = null)
    {
        if (_exitRequested) return;
        var outcome = result is null ? UiText.Get("CleanupFailed")
            : UiText.Get(result.Errors.Count == 0 ? "CleanupSucceeded" : "PartialCleanup");
        var details = result is null ? failure ?? UiText.Get("CleanupFailed")
            : UiText.Get("CleanupHistoryResult", MemorySnapshot.Bytes(result.FreedBytes), duration.TotalSeconds)
                + (result.Errors.Count == 0 ? "" : UiText.Get("CleanupHistoryErrors", result.Errors.Count));
        _cleanupHistory.Insert(0, new CleanupHistoryEntry(
            UiText.Get(manual ? "CleanupHistoryManual" : "CleanupHistoryAutomatic", DateTime.Now), outcome, details));
        if (_cleanupHistory.Count > 5) _cleanupHistory.RemoveAt(5);
        CleanupHistoryCard.Visibility = Visibility.Visible;
    }
}
