using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DigitalBrain;

namespace IntoChat.OS.Modules.AI.Memory;

[Neuron("ai.knowledge-store", State = "knowledge")]
public sealed class KnowledgeStoreNeuron : Neuron<KnowledgeStoreState>, IKnowledgeStore
{
    private const int MaxNodes = 1024;
    private const int MaxEdges = 4096;

    public async Task RecordNodeAsync(KnowledgeNode node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(node.Id);
        ct.ThrowIfCancellationRequested();

        var now = Context.Time.GetUtcNow();
        var record = new KnowledgeNodeRecord
        {
            Id = node.Id.Trim(),
            Label = node.Label?.Trim() ?? node.Id.Trim(),
            Type = node.Type?.Trim() ?? "entity",
            Properties = (node.Properties ?? new Dictionary<string, string>(StringComparer.Ordinal))
                .Select(p => new KnowledgePropertyRecord { Key = p.Key, Value = p.Value })
                .ToArray(),
            Vector = node.Vector,
            Importance = Math.Clamp(node.Importance, 0.1, 10.0),
            CreatedAt = node.CreatedAt == default ? now : node.CreatedAt,
            UpdatedAt = now,
            Writer = Id.ToString(),
            Caller = StampedCaller(),
            StampedAt = now
        };

        var existing = State.Nodes.Where(n => !string.Equals(n.Id, record.Id, StringComparison.Ordinal)).ToList();
        existing.Add(record);
        var nodes = existing.TakeLast(MaxNodes).ToArray();

        var next = new KnowledgeStoreState(State.Revision + 1, nodes, State.Edges);
        await Save(next, new KnowledgeGraphChanged(next.Revision, next.Nodes.Length, next.Edges.Length, "store-node", record.Id)).ConfigureAwait(false);
    }

    public Task<KnowledgeNode?> GetAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(id))
        {
            return Task.FromResult<KnowledgeNode?>(null);
        }

        var record = State.Nodes.FirstOrDefault(n => string.Equals(n.Id, id, StringComparison.Ordinal));
        if (record == null)
        {
            return Task.FromResult<KnowledgeNode?>(null);
        }

        return Task.FromResult<KnowledgeNode?>(ToModel(record));
    }

    public async Task DeleteNodeAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var nodes = State.Nodes.Where(n => !string.Equals(n.Id, id, StringComparison.Ordinal)).ToArray();
        // Cascading deletion: remove all incident edges
        var edges = State.Edges.Where(e => !string.Equals(e.SourceId, id, StringComparison.Ordinal)
                                        && !string.Equals(e.TargetId, id, StringComparison.Ordinal)).ToArray();

        var next = new KnowledgeStoreState(State.Revision + 1, nodes, edges);
        await Save(next, new KnowledgeGraphChanged(next.Revision, next.Nodes.Length, next.Edges.Length, "delete-node", id)).ConfigureAwait(false);
    }

    public async Task RecordEdgeAsync(KnowledgeEdge edge, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentException.ThrowIfNullOrWhiteSpace(edge.SourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(edge.TargetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(edge.Relation);
        ct.ThrowIfCancellationRequested();

        var now = Context.Time.GetUtcNow();
        var record = new KnowledgeEdgeRecord
        {
            SourceId = edge.SourceId.Trim(),
            TargetId = edge.TargetId.Trim(),
            Relation = edge.Relation.Trim(),
            Weight = Math.Clamp(edge.Weight, 0.01, 100.0),
            Metadata = (edge.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal))
                .Select(m => new KnowledgePropertyRecord { Key = m.Key, Value = m.Value })
                .ToArray(),
            CreatedAt = edge.CreatedAt == default ? now : edge.CreatedAt,
            Writer = Id.ToString(),
            Caller = StampedCaller(),
            StampedAt = now
        };

        var existing = State.Edges.Where(e => !(string.Equals(e.SourceId, record.SourceId, StringComparison.Ordinal)
                                             && string.Equals(e.TargetId, record.TargetId, StringComparison.Ordinal)
                                             && string.Equals(e.Relation, record.Relation, StringComparison.OrdinalIgnoreCase))).ToList();
        existing.Add(record);
        var edges = existing.TakeLast(MaxEdges).ToArray();

        var next = new KnowledgeStoreState(State.Revision + 1, State.Nodes, edges);
        await Save(next, new KnowledgeGraphChanged(next.Revision, next.Nodes.Length, next.Edges.Length, "store-edge")).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<KnowledgeEdge>> QueryEdgesAsync(string nodeId, EdgeDirection direction = EdgeDirection.Both, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return Task.FromResult<IReadOnlyList<KnowledgeEdge>>([]);
        }

        var edges = State.Edges.AsEnumerable();
        edges = direction switch
        {
            EdgeDirection.Outgoing => edges.Where(e => string.Equals(e.SourceId, nodeId, StringComparison.Ordinal)),
            EdgeDirection.Incoming => edges.Where(e => string.Equals(e.TargetId, nodeId, StringComparison.Ordinal)),
            _ => edges.Where(e => string.Equals(e.SourceId, nodeId, StringComparison.Ordinal) || string.Equals(e.TargetId, nodeId, StringComparison.Ordinal))
        };

        var list = edges.Select(ToModel).ToList();
        return Task.FromResult<IReadOnlyList<KnowledgeEdge>>(list);
    }

    public async Task DeleteEdgeAsync(string sourceId, string targetId, string relation, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(relation))
        {
            return;
        }

        var edges = State.Edges.Where(e => !(string.Equals(e.SourceId, sourceId, StringComparison.Ordinal)
                                          && string.Equals(e.TargetId, targetId, StringComparison.Ordinal)
                                          && string.Equals(e.Relation, relation, StringComparison.OrdinalIgnoreCase))).ToArray();

        var next = new KnowledgeStoreState(State.Revision + 1, State.Nodes, edges);
        await Save(next, new KnowledgeGraphChanged(next.Revision, next.Nodes.Length, next.Edges.Length, "delete-edge")).ConfigureAwait(false);
    }

    public Task<KnowledgeGraphView> QueryGraphAsync(string rootId, int depth = 2, CancellationToken ct = default)
        => GetGraphViewAsync(rootId, depth, ct);

    public Task<KnowledgeGraphView> GetGraphViewAsync(string? rootId = null, int depth = 2, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(rootId))
        {
            var allNodes = State.Nodes.Select(ToModel).ToList();
            var allEdges = State.Edges.Select(ToModel).ToList();
            return Task.FromResult(new KnowledgeGraphView(allNodes, allEdges, rootId));
        }

        if (!State.Nodes.Any(n => string.Equals(n.Id, rootId, StringComparison.Ordinal)))
        {
            return Task.FromResult(new KnowledgeGraphView([], [], rootId));
        }

        var maxDepth = Math.Clamp(depth, 0, 10);
        var visitedNodeIds = new HashSet<string>(StringComparer.Ordinal) { rootId };
        var traversedEdges = new List<KnowledgeEdge>();
        var currentLevel = new HashSet<string>(StringComparer.Ordinal) { rootId };

        for (var d = 0; d < maxDepth && currentLevel.Count > 0; d++)
        {
            var nextLevel = new HashSet<string>(StringComparer.Ordinal);
            foreach (var currentId in currentLevel)
            {
                foreach (var edgeRecord in State.Edges)
                {
                    if (string.Equals(edgeRecord.SourceId, currentId, StringComparison.Ordinal))
                    {
                        var edge = ToModel(edgeRecord);
                        traversedEdges.Add(edge);
                        if (visitedNodeIds.Add(edgeRecord.TargetId))
                        {
                            nextLevel.Add(edgeRecord.TargetId);
                        }
                    }
                    else if (string.Equals(edgeRecord.TargetId, currentId, StringComparison.Ordinal))
                    {
                        var edge = ToModel(edgeRecord);
                        traversedEdges.Add(edge);
                        if (visitedNodeIds.Add(edgeRecord.SourceId))
                        {
                            nextLevel.Add(edgeRecord.SourceId);
                        }
                    }
                }
            }
            currentLevel = nextLevel;
        }

        var resultNodes = State.Nodes
            .Where(n => visitedNodeIds.Contains(n.Id))
            .Select(ToModel)
            .ToList();

        var distinctEdges = traversedEdges
            .GroupBy(e => (e.SourceId, e.TargetId, e.Relation))
            .Select(g => g.First())
            .ToList();

        return Task.FromResult(new KnowledgeGraphView(resultNodes, distinctEdges, rootId));
    }

    // Hybrid recall over this store's own graph: lexical and vector seeds, then personalized PageRank from the seeds along the
    // relations, then the shortest path back to a seed as the explanation. Linear in nodes and edges; the snapshot is cached by revision.
    public Task<IReadOnlyList<KnowledgeRecallHit>> SemanticSearchAsync(string query, int limit = 10, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(query) || limit <= 0 || State.Nodes.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<KnowledgeRecallHit>>([]);
        }

        var terms = Tokenize(query);
        var graph = Graph();
        var seeds = new float[graph.NodeCount];
        var seeded = new List<int>();
        for (var i = 0; i < State.Nodes.Length; i++)
        {
            var node = State.Nodes[i];
            var text = $"{node.Label} {node.Type} {string.Join(" ", node.Properties.Select(p => $"{p.Key} {p.Value}"))}";
            var score = (float)LexicalScore(text, terms, node.Importance);
            if (score > 0) { seeds[i] = score; seeded.Add(i); }
        }
        if (seeded.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<KnowledgeRecallHit>>([]);
        }
        // Nodes that look like a seed (cosine over stored vectors) join the seed set at half weight.
        foreach (var s in seeded.ToArray())
        {
            if (State.Nodes[s].Vector is not { Length: > 0 } seedVector) { continue; }
            for (var i = 0; i < State.Nodes.Length; i++)
            {
                if (i == s || seeds[i] > 0 || State.Nodes[i].Vector is not { Length: > 0 } candidate || candidate.Length != seedVector.Length) { continue; }
                var similarity = InMemoryKnowledgeStore.CosineSimilarity(seedVector, candidate);
                if (similarity > 0.5) { seeds[i] = (float)(seeds[s] * similarity * 0.5); }
            }
        }

        var rank = DigitalBrain.Graph.GraphCompute.PageRank(graph, seeds);
        var scored = new List<(int Index, double Score)>(graph.NodeCount);
        for (var i = 0; i < graph.NodeCount; i++)
        {
            var spread = rank[i] * graph.NodeCount;
            var score = (0.6 * seeds[i]) + (0.3 * spread) + (0.1 * State.Nodes[i].Importance / 10.0);
            if (seeds[i] > 0 || spread > 1.0) { scored.Add((i, (double)score)); }
        }
        var hits = new List<KnowledgeRecallHit>();
        foreach (var (index, score) in scored.OrderByDescending(x => x.Score).ThenBy(x => State.Nodes[x.Index].Id, StringComparer.Ordinal).Take(limit))
        {
            IReadOnlyList<string>? path = null;
            if (seeds[index] <= 0)
            {
                path = seeded
                    .Select(s => DigitalBrain.Graph.GraphCompute.ShortestPath(graph, s, index, 3))
                    .Where(p => p is not null)
                    .MinBy(p => p!.Nodes.Length)?.Nodes;
            }
            hits.Add(new KnowledgeRecallHit(ToModel(State.Nodes[index]), score, path));
        }
        return Task.FromResult<IReadOnlyList<KnowledgeRecallHit>>(hits);
    }

    private DigitalBrain.Graph.GraphSnapshot? _graph;

    // The store's own graph in compressed sparse row form: node i is State.Nodes[i]; relations are edges with their weight.
    private DigitalBrain.Graph.GraphSnapshot Graph()
    {
        if (_graph is { } cached && cached.Revision == State.Revision) { return cached; }
        var sink = new DigitalBrain.Graph.GraphSink();
        foreach (var node in State.Nodes)
        {
            sink.Node(node.Id, string.Equals(node.Type, "entity", StringComparison.OrdinalIgnoreCase) ? DigitalBrain.Graph.GraphNodeKind.Entity : DigitalBrain.Graph.GraphNodeKind.Fact, node.Label);
        }
        foreach (var edge in State.Edges)
        {
            var from = sink.IndexOf(edge.SourceId);
            var to = sink.IndexOf(edge.TargetId);
            if (from >= 0 && to >= 0) { sink.Edge(from, to, DigitalBrain.Graph.GraphEdgeKind.Relates, (float)edge.Weight); }
        }
        _graph = DigitalBrain.Graph.GraphCompute.Build(sink, State.Revision);
        return _graph;
    }

    private static string? StampedCaller()
        => IntoChat.OS.Framework.CallerContextStamper.TryGet(out var caller) ? caller.PrincipalId : null;

    private static KnowledgeNode ToModel(KnowledgeNodeRecord r) => new(
        r.Id,
        r.Label,
        r.Type,
        r.Properties.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
        r.Vector,
        r.Importance,
        r.CreatedAt,
        r.UpdatedAt,
        r.Writer is { } writer ? new Provenance(writer, r.Caller, r.StampedAt) : null);

    private static KnowledgeEdge ToModel(KnowledgeEdgeRecord r) => new(
        r.SourceId,
        r.TargetId,
        r.Relation,
        r.Weight,
        r.Metadata.ToDictionary(m => m.Key, m => m.Value, StringComparer.Ordinal),
        r.CreatedAt,
        r.Writer is { } writer ? new Provenance(writer, r.Caller, r.StampedAt) : null);

    private static string[] Tokenize(string text)
        => text.Split([' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '"', '\'', '(', ')', '[', ']', '{', '}'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length >= 2)
            .Select(w => w.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static double LexicalScore(string document, string[] queryTerms, double importance)
    {
        if (queryTerms.Length == 0)
        {
            return 0;
        }

        var matched = queryTerms.Count(term => document.Contains(term, StringComparison.OrdinalIgnoreCase));
        if (matched == 0)
        {
            return 0;
        }

        var lexical = (double)matched / queryTerms.Length;
        return lexical * (1.0 + (importance / 10.0));
    }
}
