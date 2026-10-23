using System.Diagnostics;
using Microsoft.UI.Dispatching;

namespace MemReduct.Fluent;

public sealed partial class MainWindow
{
    private DispatcherQueueTimer? _metricsTimer;
    private MemorySnapshot? _latestMetrics;
    private (double Physical, double Commit, double Cache, double CachePercent) _shownMetrics, _metricsFrom;
    private long _metricsStarted;
    private bool _metricsCleaning, _metricsToZero, _metricsReturning;

    private bool CanAnimateMetrics => _uiSettings.AnimationsEnabled && !_accessibility.HighContrast
        && CanRenderWindow && !_sampleFailed;

    private static (double Physical, double Commit, double Cache, double CachePercent) MetricsFor(MemorySnapshot sample)
        => (sample.PhysicalPercent, sample.CommitPercent, sample.CacheCurrent ?? 0,
            sample.CacheCurrent is { } used && sample.CachePeak is { } peak ? MemorySnapshot.Percent(used, peak) : 0);

    private void UpdateAnimatedMetrics(MemorySnapshot sample)
    {
        _latestMetrics = sample;
        if (!CanAnimateMetrics) { StopMetricsAnimation(); return; }
        if (!_metricsCleaning && !_metricsToZero && !_metricsReturning) DrawMetrics(MetricsFor(sample));
    }

    private void BeginMetricsCleanup()
    {
        if (_latestMetrics is null) return;
        _metricsCleaning = true;
        SetText(PressureText, UiText.Get("Cleaning"));
        StartMetricsMotion(toZero: true);
    }

    private void EndMetricsCleanup()
    {
        if (!_metricsCleaning) return;
        _metricsCleaning = false;
        // Fast operations still finish the short downward phase before returning to real data.
        if (!_metricsToZero || _metricsTimer?.IsRunning != true) StartMetricsMotion(toZero: false);
        if (!_exitRequested && !_sampleFailed && _latestMetrics is { } sample)
            SetText(PressureText, UiText.Get(sample.PhysicalPercent >= 90 ? "PressureHigh"
                : sample.PhysicalPercent >= 70 ? "PressureElevated" : "PressureNormal"));
    }

    private void StartMetricsMotion(bool toZero)
    {
        _metricsToZero = toZero;
        _metricsReturning = !toZero;
        _metricsFrom = _shownMetrics;
        _metricsStarted = Stopwatch.GetTimestamp();
        if (!CanAnimateMetrics) { StopMetricsAnimation(); return; }
        if (_metricsTimer is null)
        {
            _metricsTimer = DispatcherQueue.CreateTimer();
            _metricsTimer.Interval = TimeSpan.FromMilliseconds(16);
            _metricsTimer.Tick += (_, _) =>
            {
                if (!CanAnimateMetrics) { StopMetricsAnimation(); return; }
                var target = _metricsToZero || _latestMetrics is null ? default : MetricsFor(_latestMetrics);
                var progress = Math.Clamp(Stopwatch.GetElapsedTime(_metricsStarted).TotalMilliseconds
                    / (_metricsToZero ? 250 : 850), 0, 1);
                var eased = 1 - Math.Pow(1 - progress, 3);
                DrawMetrics((
                    _metricsFrom.Physical + (target.Physical - _metricsFrom.Physical) * eased,
                    _metricsFrom.Commit + (target.Commit - _metricsFrom.Commit) * eased,
                    _metricsFrom.Cache + (target.Cache - _metricsFrom.Cache) * eased,
                    _metricsFrom.CachePercent + (target.CachePercent - _metricsFrom.CachePercent) * eased));
                if (progress < 1) return;
                _metricsTimer.Stop();
                if (_metricsToZero)
                {
                    _metricsToZero = false;
                    if (!_metricsCleaning) StartMetricsMotion(toZero: false);
                }
                else _metricsReturning = false;
            };
        }
        _metricsTimer.Start();
    }

    private void StopMetricsAnimation()
    {
        _metricsTimer?.Stop();
        _metricsToZero = _metricsReturning = false;
        if (!_exitRequested && _latestMetrics is { } sample) DrawMetrics(MetricsFor(sample));
    }

    private void DrawMetrics((double Physical, double Commit, double Cache, double CachePercent) frame)
    {
        _shownMetrics = frame;
        SetText(PhysicalValue, $"{frame.Physical:0.0}%");
        SetText(CommitValue, $"{frame.Commit:0.0}%");
        SetText(CacheValue, _latestMetrics?.CacheCurrent is not null
            ? MemorySnapshot.Bytes((ulong)Math.Max(0, frame.Cache)) : "—");
        SetProgress(PhysicalBar, frame.Physical);
        SetProgress(CommitBar, frame.Commit);
        SetProgress(CacheBar, frame.CachePercent);
    }
}
