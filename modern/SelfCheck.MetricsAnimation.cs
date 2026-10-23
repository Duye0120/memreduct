using System.Diagnostics;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace MemReduct.Fluent;

public sealed partial class MainWindow
{
    private bool _metricsPreviewBusy;
    private string? _metricsPreviewDirectory;

    private async Task CheckMetricsAnimationAsync()
    {
        var original = _latestMetrics;
        var realPercent = _lastPercent;
        var latest = new MemorySnapshot(100, 38, 90, 100, 128UL * 1024 * 1024, 1024UL * 1024 * 1024);
        UpdateStats(new MemorySnapshot(100, 25, 80, 100, 256UL * 1024 * 1024, latest.CachePeak));
        BeginMetricsCleanup();
        UpdateStats(latest);
        if (CanAnimateMetrics)
        {
            await Task.Delay(350);
            Require(PhysicalValue.Text == $"{0:0.0}%" && CommitValue.Text == $"{0:0.0}%"
                && PhysicalBar.Value == 0 && CommitBar.Value == 0 && CacheBar.Value == 0,
                "Cleanup numbers and all three bars reach zero together");
        }
        EndMetricsCleanup();
        await Task.Delay(950);
        Require(PhysicalValue.Text == $"{62:0.0}%" && CommitValue.Text == $"{90:0.0}%"
            && CacheValue.Text == "128 MB" && PhysicalBar.Value == 62 && CommitBar.Value == 90
            && CacheBar.Value == 12.5 && !_metricsReturning && _lastPercent == realPercent,
            "Animation restores the newest real sample without changing the scheduling/tray reading");

        BeginMetricsCleanup(); EndMetricsCleanup();
        await Task.Delay(1200);
        Require(!_metricsToZero && !_metricsReturning && PhysicalBar.Value == 62,
            "A fast cleanup completes both animation phases without a stuck timer");
        BeginMetricsCleanup();
        UpdateStats(latest with { CacheCurrent = null });
        SetSamplingState(true);
        Require(PhysicalBar.Value == 62 && CacheValue.Text == "—" && _metricsTimer?.IsRunning != true,
            "Sampling failure cancels the visual zero and preserves missing-cache semantics");
        EndMetricsCleanup(); SetSamplingState(false);
        if (original is not null) UpdateStats(original);
    }

    // Isolated visible preview: no preferences, tray, authorization or native cleanup.
    internal async void RunAnimationPreview(string directory)
    {
        _metricsPreviewDirectory = directory;
        Directory.CreateDirectory(directory);
        AppTitleBar.Title = "Mem Reduct 动画预览";
        Title = AppTitleBar.Title;
        var scale = Shell.XamlRoot?.RasterizationScale ?? 1;
        AppWindow.Resize(new SizeInt32((int)(880 * scale), (int)(740 * scale)));
        AppWindow.Move(new PointInt32(100, 100));
        Shell.SizeChanged += (_, _) => ReflowMetrics(Shell.ActualWidth, _uiSettings.TextScaleFactor);
        ReflowMetrics(880, _uiSettings.TextScaleFactor);
        UpdateStats(MemoryEngine.Read());
        CleanButton.Content = UiText.Get("CleanMemory");
        Activate();
        await Task.Delay(500);
        await WritePreview(directory, "memory-layout.png");
        await PreviewMetricsCleanupAsync();
    }

    private async Task PreviewMetricsCleanupAsync()
    {
        if (_metricsPreviewBusy || _exitRequested) return;
        _metricsPreviewBusy = true;
        CleanButton.IsEnabled = false;
        CleanButton.Content = UiText.Get("Cleaning");
        try
        {
            var capture = CaptureMetricsFramesAsync();
            BeginMetricsCleanup();
            await Task.Delay(700);
            UpdateStats(MemoryEngine.Read());
            EndMetricsCleanup();
            await capture;
        }
        finally
        {
            EndMetricsCleanup();
            CleanButton.Content = UiText.Get("CleanMemory");
            CleanButton.IsEnabled = true;
            _metricsPreviewBusy = false;
        }
    }

    private async Task CaptureMetricsFramesAsync()
    {
        if (_metricsPreviewDirectory is null) return;
        var elapsed = Stopwatch.StartNew();
        for (var index = 0; index < 26 && !_exitRequested; index++)
        {
            await WritePreview(_metricsPreviewDirectory, $"frame-{index:00}.png", MetricsCard, waitMilliseconds: 0);
            await Task.Delay((int)Math.Max(0, (index + 1) * 100 - elapsed.ElapsedMilliseconds));
        }
    }
}
