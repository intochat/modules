using DigitalBrain;
using DigitalBrain.Graph;
using DigitalBrain.Runtime.Orleans.Hosting;

namespace IntoChat.OS.Modules.AI.Memory;

// Puts a brain's memory facts and relations into its brain graph: a Fact or Entity node per knowledge node, owned by the brain,
// Relates edges with their weight, and a Mentions edge when a fact names a neuron that is already in the graph (property "neuron").
// Reads the store through its contract, so the caller stamp and brain scope of the graph rebuild apply.
internal sealed class KnowledgeGraphSource(IServiceProvider services) : IGraphSource
{
    public const string StoreKey = "memory";

    public async Task Contribute(BrainId brain, GraphSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        KnowledgeGraphView view;
        try
        {
            view = await SiloNeurons.Get<IKnowledgeStore>(services, $"{brain.Value}/{StoreKey}").GetGraphViewAsync(null, 10, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return; // no knowledge store is hosted for this brain
        }
        var store = $"{brain.Value}/ai.knowledge-store/{StoreKey}";
        var root = sink.IndexOf(brain.Value);
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var node in view.Nodes)
        {
            var kind = string.Equals(node.Type, "entity", StringComparison.OrdinalIgnoreCase) ? GraphNodeKind.Entity : GraphNodeKind.Fact;
            var i = sink.Node($"{store}#node/{node.Id}", kind, node.Label);
            index[node.Id] = i;
            if (root >= 0) { sink.Edge(root, i, GraphEdgeKind.Owns, (float)node.Importance); }
            if (node.Properties.TryGetValue("neuron", out var neuron) && sink.IndexOf(neuron) is var target and >= 0)
            {
                sink.Edge(i, target, GraphEdgeKind.Mentions);
            }
        }
        foreach (var edge in view.Edges)
        {
            if (index.TryGetValue(edge.SourceId, out var from) && index.TryGetValue(edge.TargetId, out var to))
            {
                sink.Edge(from, to, GraphEdgeKind.Relates, (float)edge.Weight);
            }
        }
    }
}
