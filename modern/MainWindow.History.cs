using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace MemReduct.Fluent;

public sealed partial class MainWindow
{
    private readonly MemoryHistory _history = new();

    private void HistoryPlot_SizeChanged(object sender, SizeChangedEventArgs args) => RenderHistory();

    private void RenderHistory()
    {
        if (HistoryPlot is null || OverviewPage.Visibility != Visibility.Visible || HistoryPlot.ActualWidth <= 0) return;
        var samples = _history.Samples.ToArray();
        HistoryEmpty.Visibility = samples.Length < 2 ? Visibility.Visible : Visibility.Collapsed;
        var physical = new PathGeometry();
        var committed = new PathGeometry();
        PointCollection? physicalPoints = null, commitPoints = null;
        var end = samples.Length == 0 ? DateTimeOffset.UtcNow : samples[^1].Time;
        DateTimeOffset? previous = null;
        foreach (var sample in samples)
        {
            var x = MemoryHistory.Position(sample.Time, end, HistoryPlot.ActualWidth);
            var a = new Point(x, (1 - sample.Physical / 100) * HistoryPlot.ActualHeight);
            var b = new Point(x, (1 - sample.Commit / 100) * HistoryPlot.ActualHeight);
            if (previous is null || sample.Time - previous.Value > TimeSpan.FromSeconds(5))
            {
                var physicalFigure = new PathFigure { StartPoint = a, IsClosed = false, IsFilled = false };
                var commitFigure = new PathFigure { StartPoint = b, IsClosed = false, IsFilled = false };
                var physicalLine = new PolyLineSegment(); var commitLine = new PolyLineSegment();
                physicalPoints = physicalLine.Points; commitPoints = commitLine.Points;
                physicalFigure.Segments.Add(physicalLine); commitFigure.Segments.Add(commitLine);
                physical.Figures.Add(physicalFigure); committed.Figures.Add(commitFigure);
            }
            else
            {
                physicalPoints!.Add(a);
                commitPoints!.Add(b);
            }
            previous = sample.Time;
        }
        PhysicalHistoryPath.Data = physical;
        CommitHistoryPath.Data = committed;
        if (samples.Length > 0)
        {
            SetText(HistorySummary, UiText.Get("HistorySummary", samples.Min(x => x.Physical), samples.Max(x => x.Physical)));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(HistoryPlot, HistorySummary.Text);
        }
    }
}
