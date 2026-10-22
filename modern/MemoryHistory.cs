namespace MemReduct.Fluent;

internal readonly record struct MemorySample(DateTimeOffset Time, double Physical, double Commit);

internal sealed class MemoryHistory
{
    internal static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
    internal const int Capacity = 301;
    private readonly Queue<MemorySample> _samples = new(Capacity + 1);
    private DateTimeOffset _lastTime;
    internal IReadOnlyCollection<MemorySample> Samples => _samples;

    internal void Record(MemorySnapshot snapshot, DateTimeOffset time)
    {
        if (time < _lastTime) _samples.Clear();
        _lastTime = time;
        _samples.Enqueue(new(time, snapshot.PhysicalPercent, snapshot.CommitPercent));
        while (_samples.Count > Capacity || (_samples.TryPeek(out var first) && first.Time < time - Window))
            _samples.Dequeue();
    }

    internal static double Position(DateTimeOffset time, DateTimeOffset end, double width) =>
        Math.Clamp(1 - (end - time).TotalSeconds / Window.TotalSeconds, 0, 1) * Math.Max(0, width);
}
