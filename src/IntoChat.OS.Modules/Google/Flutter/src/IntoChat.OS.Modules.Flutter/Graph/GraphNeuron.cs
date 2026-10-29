using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Graph.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Graph;

[Neuron(UIVocabulary.GraphType)]
internal sealed class GraphNeuron
    : UiNeuron<GraphState>, IGraph
{
    public Task Render(string title, IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Title = title.Trim();
        next.Nodes = [.. nodes];
        next.Edges = [.. edges];
        return Save(next, new GraphChanged(FullKey, next.Version));
    }

    [DigitalBrain.ReadOnlyCall] public Task<GraphState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
