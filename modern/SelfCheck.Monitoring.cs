using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MemReduct.Fluent;

public sealed partial class MainWindow
{
    private async Task CheckSamplingStateAsync(MemorySnapshot memory, string directory)
    {
        var theme = Shell.RequestedTheme;
        ReportSamplingFailure(new InvalidOperationException("Self-check fixture: sampling is unavailable."));
        var unavailable = GetTrayState();
        Require(_sampleFailed && MonitoringStatus.Visibility == Visibility.Visible && LiveLabel.Text == UiText.Get("MonitoringUnavailable")
            && PressureText.Text == UiText.Get("NoReading") && !CanStartAutomaticCleanup
            && unavailable.Percent is null && unavailable.Feedback == CleanupFeedback.Failed,
            "A failed first sample cannot advertise live data or a fabricated tray percentage");

        _lastReadUtc = DateTimeOffset.UtcNow.AddSeconds(-10);
        _lastPercent = memory.PhysicalPercent;
        Shell.RequestedTheme = ElementTheme.Light;
        UpdateStats(memory);
        var retainedValue = PhysicalValue.Text;
        SetSamplingState(true);
        Require(PhysicalValue.Text == retainedValue && PressureText.Text == UiText.Get("LastReading")
            && HistoryEnd.Text == UiText.Get("LastReading")
            && Equals(ToolTipService.GetToolTip(LiveLabel), UiText.Get("LastReadingTime", _lastReadUtc.Value.ToLocalTime()))
            && Microsoft.UI.Xaml.Automation.AutomationProperties.GetHelpText(LiveLabel) == ToolTipService.GetToolTip(LiveLabel)?.ToString(),
            "Retained readings identify their last successful time and do not label a stale graph endpoint as now");
        Layout(700, 780, 1);
        await WritePreview(directory, "sampling-failure-light.png");
        Shell.RequestedTheme = ElementTheme.Dark;
        SetSamplingState(true);
        await WritePreview(directory, "sampling-failure-dark.png");
        Shell.RequestedTheme = theme;

        SetFeedback(CleanupFeedback.Cleaning, UiText.Get("Cleaning"));
        var busy = GetTrayState();
        Require(busy.Percent is null && busy.Feedback == CleanupFeedback.Cleaning && busy.Frame >= 0
            && busy.Status.Contains(UiText.Get("MonitoringUnavailable")) && busy.Status.Contains(UiText.Get("Cleaning")),
            "Cleanup animation preserves the unavailable reading and explains both states");
        SetFeedback(CleanupFeedback.None, "");

        ReportIssue(UiText.Get("SaveFailed"), "Self-check fixture: unrelated failure.");
        Sample();
        Require(!_sampleFailed && MonitoringStatus.Visibility == Visibility.Collapsed
            && LiveLabel.Text == UiText.Get("LiveLabel.Text") && GetTrayState().Percent is not null
            && IssueBanner.IsOpen && IssueBanner.Title == UiText.Get("SaveFailed"),
            "Successful read-only sampling restores live data without dismissing an unrelated error");
        ReportSamplingFailure(new InvalidOperationException("Self-check fixture: second sampling interruption."));
        Sample();
        Require(!_sampleFailed && !IssueBanner.IsOpen && HistoryEnd.Text == UiText.Get("HistoryEnd.Text")
            && ToolTipService.GetToolTip(LiveLabel) is null
            && Microsoft.UI.Xaml.Automation.AutomationProperties.GetHelpText(LiveLabel) == "",
            "Recovery clears its own error, stale timestamp and unavailable endpoint");
    }
}
