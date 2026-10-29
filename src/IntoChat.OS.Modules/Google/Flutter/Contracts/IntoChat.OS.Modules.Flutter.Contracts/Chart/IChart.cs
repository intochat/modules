using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Chart;

[NeuronAlias("chart"), DefaultNeuronType(UIVocabulary.ChartType)]
public interface IChart
{
    Task Render(string title, string kind, IReadOnlyList<ChartPoint> points);
    Task Append(ChartPoint point);
    [DigitalBrain.ReadOnlyCall] Task<ChartState> Read();
}

public sealed record ChartPoint(
    string EventId,
    string Label,
    double Value);

[ExportableState]
public sealed class ChartState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "bar";
    public List<ChartPoint> Points { get; set; } = [];
}
