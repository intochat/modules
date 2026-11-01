using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IntoChat.OS.Modules.AI.Memory;

public sealed class InMemoryKnowledgeStore : IKnowledgeStore
{
    private readonly ConcurrentDictionary<string, KnowledgeNode> _nodes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, KnowledgeEdge> _edges = new(StringComparer.Ordinal);

    public Task RecordNodeAsync(KnowledgeNode node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(node.Id);

        var now = DateTimeOffset.UtcNow;
        var updated = node with
        {
            CreatedAt = node.CreatedAt == default ? now : node.CreatedAt,
            UpdatedAt = now
        };

        _nodes[node.Id] = updated;
        return Task.CompletedTask;
    }

    public Task<KnowledgeNode?> GetAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Task.FromResult<KnowledgeNode?>(null);
        }

        _nodes.TryGetValue(id, out var node);
        return Task.FromResult(node);
    }

    public Task DeleteNodeAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Task.CompletedTask;
        }

        _nodes.TryRemove(id, out _);

        // Cascading deletion: remove all incident edges
        foreach (var key in _edges.Keys)
        {
            if (_edges.TryGetValue(key, out var edge) &&
                (string.Equals(edge.SourceId, id, StringComparison.Ordinal) || string.Equals(edge.TargetId, id, StringComparison.Ordinal)))
            {
                _edges.TryRemove(key, out _);
            }
        }

        return Task.CompletedTask;
    }

    public Task RecordEdgeAsync(KnowledgeEdge edge, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentException.ThrowIfNullOrWhiteSpace(edge.SourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(edge.TargetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(edge.Relation);

        var key = EdgeKey(edge.SourceId, edge.TargetId, edge.Relation);
        var updated = edge with
        {
            CreatedAt = edge.CreatedAt == default ? DateTimeOffset.UtcNow : edge.CreatedAt
        };

        _edges[key] = updated;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<KnowledgeEdge>> QueryEdgesAsync(string nodeId, EdgeDirection direction = EdgeDirection.Both, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return Task.FromResult<IReadOnlyList<KnowledgeEdge>>([]);
        }

        var edges = _edges.Values.AsEnumerable();
        edges = direction switch
        {
            EdgeDirection.Outgoing => edges.Where(e => string.Equals(e.SourceId, nodeId, StringComparison.Ordinal)),
            EdgeDirection.Incoming => edges.Where(e => string.Equals(e.TargetId, nodeId, StringComparison.Ordinal)),
            _ => edges.Where(e => string.Equals(e.SourceId, nodeId, StringComparison.Ordinal) || string.Equals(e.TargetId, nodeId, StringComparison.Ordinal))
        };

        return Task.FromResult<IReadOnlyList<KnowledgeEdge>>(edges.ToList());
    }

    public Task DeleteEdgeAsync(string sourceId, string targetId, string relation, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(relation))
        {
            return Task.CompletedTask;
        }

        var key = EdgeKey(sourceId, targetId, relation);
        _edges.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<KnowledgeGraphView> GetGraphViewAsync(string? rootId = null, int depth = 2, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rootId) || !_nodes.ContainsKey(rootId))
        {
            return Task.FromResult(new KnowledgeGraphView(_nodes.Values.ToList(), _edges.Values.ToList(), rootId));
        }

        var maxDepth = Math.Clamp(depth, 1, 5);
        var visitedNodeIds = new HashSet<string>(StringComparer.Ordinal) { rootId };
        var traversedEdges = new List<KnowledgeEdge>();
        var currentLevel = new HashSet<string>(StringComparer.Ordinal) { rootId };

        for (var d = 0; d < maxDepth && currentLevel.Count > 0; d++)
        {
            var nextLevel = new HashSet<string>(StringComparer.Ordinal);
            foreach (var currentId in currentLevel)
            {
                foreach (var edge in _edges.Values)
                {
                    if (string.Equals(edge.SourceId, currentId, StringComparison.Ordinal))
                    {
                        traversedEdges.Add(edge);
                        if (visitedNodeIds.Add(edge.TargetId))
                        {
                            nextLevel.Add(edge.TargetId);
                        }
                    }
                    else if (string.Equals(edge.TargetId, currentId, StringComparison.Ordinal))
                    {
                        traversedEdges.Add(edge);
                        if (visitedNodeIds.Add(edge.SourceId))
                        {
                            nextLevel.Add(edge.SourceId);
                        }
                    }
                }
            }
            currentLevel = nextLevel;
        }

        var resultNodes = _nodes.Values
            .Where(n => visitedNodeIds.Contains(n.Id))
            .ToList();

        var distinctEdges = traversedEdges
            .GroupBy(e => (e.SourceId, e.TargetId, e.Relation))
            .Select(g => g.First())
            .ToList();

        return Task.FromResult(new KnowledgeGraphView(resultNodes, distinctEdges, rootId));
    }

    public Task<IReadOnlyList<KnowledgeRecallHit>> SemanticSearchAsync(string query, int limit = 10, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
        {
            return Task.FromResult<IReadOnlyList<KnowledgeRecallHit>>([]);
        }

        var terms = Tokenize(query);
        var nodeScores = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var node in _nodes.Values)
        {
            var text = $"{node.Label} {node.Type} {string.Join(" ", node.Properties.Select(p => $"{p.Key} {p.Value}"))}";
            var score = LexicalScore(text, terms, node.Importance);
            if (score > 0)
            {
                nodeScores[node.Id] = score;
            }
        }

        // Spreading activation along connected edges (decay 0.5)
        var activatedScores = new Dictionary<string, double>(nodeScores, StringComparer.Ordinal);
        foreach (var (sourceId, sourceScore) in nodeScores)
        {
            foreach (var edge in _edges.Values)
            {
                if (string.Equals(edge.SourceId, sourceId, StringComparison.Ordinal))
                {
                    var propagated = sourceScore * edge.Weight * 0.5;
                    activatedScores[edge.TargetId] = activatedScores.GetValueOrDefault(edge.TargetId) + propagated;
                }
                else if (string.Equals(edge.TargetId, sourceId, StringComparison.Ordinal))
                {
                    var propagated = sourceScore * edge.Weight * 0.5;
                    activatedScores[edge.SourceId] = activatedScores.GetValueOrDefault(edge.SourceId) + propagated;
                }
            }
        }

        var hits = new List<KnowledgeRecallHit>();
        var ranked = activatedScores
            .OrderByDescending(kv => kv.Value)
            .Take(limit);

        foreach (var (id, score) in ranked)
        {
            if (_nodes.TryGetValue(id, out var node))
            {
                hits.Add(new KnowledgeRecallHit(node, score));
            }
        }

        return Task.FromResult<IReadOnlyList<KnowledgeRecallHit>>(hits);
    }

    private static string EdgeKey(string source, string target, string relation)
        => $"{source}->{target}:{relation.ToLowerInvariant()}";

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
