using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Chart.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Chart;

[Neuron(UIVocabulary.ChartType)]
internal sealed class ChartNeuron
    : UiNeuron<ChartState>, IChart
{
    private static readonly HashSet<string> Kinds = new(StringComparer.OrdinalIgnoreCase) { "bar", "line", "pie" };

    public Task Render(string title, string kind, IReadOnlyList<ChartPoint> points)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(points);
        if (!Kinds.Contains(kind)) { throw new ArgumentOutOfRangeException(nameof(kind)); }
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Title = title.Trim();
        next.Kind = kind.Trim().ToLowerInvariant();
        next.Points = [.. points.TakeLast(UIVocabulary.ChartMaxPoints)];
        return Save(next, new ChartChanged(FullKey, next.Version, next.Title));
    }

    public Task Append(ChartPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentException.ThrowIfNullOrWhiteSpace(point.Label);
        var next = Snapshot;
        next.Name = FullKey;
        if (!string.IsNullOrWhiteSpace(point.EventId) && next.Points.Exists(existing => existing.EventId == point.EventId))
        {
            return Task.CompletedTask;
        }

        next.Version++;
        next.Points.Add(point);
        if (next.Points.Count > UIVocabulary.ChartMaxPoints)
        {
            next.Points.RemoveRange(0, next.Points.Count - UIVocabulary.ChartMaxPoints);
        }

        return Save(next, new ChartChanged(FullKey, next.Version, next.Title));
    }

    [DigitalBrain.ReadOnlyCall]
    public Task<ChartState> Read()
    {
        var state = Snapshot;
        state.Name = FullKey;
        return Task.FromResult(state);
    }
}
