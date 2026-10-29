using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Graph;

[NeuronAlias("graph"), DefaultNeuronType(UIVocabulary.GraphType)]
public interface IGraph
{
    Task Render(string title, IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges);
    [DigitalBrain.ReadOnlyCall] Task<GraphState> Read();
}

public sealed record GraphNode(string Id, string Label);

public sealed record GraphEdge(string From, string To);

[ExportableState]
public sealed class GraphState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Title { get; set; } = "";
    public List<GraphNode> Nodes { get; set; } = [];
    public List<GraphEdge> Edges { get; set; } = [];
}
