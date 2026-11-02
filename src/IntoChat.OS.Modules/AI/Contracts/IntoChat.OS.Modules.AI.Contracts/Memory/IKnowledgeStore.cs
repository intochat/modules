using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Memory;

public enum EdgeDirection
{
    Outgoing = 0,
    Incoming = 1,
    Both = 2
}

/// <summary>Who wrote a fact or relation, stamped by the store from the runtime's caller context; callers cannot set it.</summary>
/// <param name="Writer">The neuron that stored it.</param>
/// <param name="Caller">The stamped caller principal, app or person, when one was present.</param>
/// <param name="At">When it was stored.</param>
public sealed record Provenance(string Writer, string? Caller, DateTimeOffset At);

public sealed record KnowledgeNode(
    string Id,
    string Label,
    string Type,
    IReadOnlyDictionary<string, string> Properties,
    float[]? Vector = null,
    double Importance = 1.0,
    DateTimeOffset CreatedAt = default,
    DateTimeOffset UpdatedAt = default,
    Provenance? Source = null);

public sealed record KnowledgeEdge(
    string SourceId,
    string TargetId,
    string Relation,
    double Weight = 1.0,
    IReadOnlyDictionary<string, string>? Metadata = null,
    DateTimeOffset CreatedAt = default,
    Provenance? Source = null);

public sealed record KnowledgeGraphView(
    IReadOnlyList<KnowledgeNode> Nodes,
    IReadOnlyList<KnowledgeEdge> Edges,
    string? RootId = null);

public sealed record KnowledgeRecallHit(
    KnowledgeNode Node,
    double RelevanceScore,
    IReadOnlyList<string>? AssociativePath = null);

[DigitalBrain.Signal("ai.knowledge.changed")]
public sealed record KnowledgeGraphChanged(
    long Revision,
    int NodeCount,
    int EdgeCount,
    string? Mutation = null,
    string? NodeId = null) : DigitalBrain.Signal;

[NeuronAlias("ai.knowledge-store"), DefaultNeuronType("ai.knowledge-store")]
[Description("Associative knowledge graph storing typed nodes, weighted edges, and semantic properties with graph traversal.")]
public interface IKnowledgeStore
{
    // Node CRUD
    Task RecordNodeAsync(KnowledgeNode node, CancellationToken ct = default);

    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<KnowledgeNode?> GetAsync(string id, CancellationToken ct = default);

    Task DeleteNodeAsync(string id, CancellationToken ct = default);

    // Edge CRUD
    Task RecordEdgeAsync(KnowledgeEdge edge, CancellationToken ct = default);

    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<IReadOnlyList<KnowledgeEdge>> QueryEdgesAsync(string nodeId, EdgeDirection direction = EdgeDirection.Both, CancellationToken ct = default);

    Task DeleteEdgeAsync(string sourceId, string targetId, string relation, CancellationToken ct = default);

    // Subgraph & Associative Traversal
    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<KnowledgeGraphView> GetGraphViewAsync(string? rootId = null, int depth = 2, CancellationToken ct = default);

    // Hybrid Semantic & Associative Retrieval
    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<IReadOnlyList<KnowledgeRecallHit>> SemanticSearchAsync(string query, int limit = 10, CancellationToken ct = default);

    // Convenience aliases
    Task<KnowledgeGraphView> QueryGraphAsync(string rootId, int depth = 2, CancellationToken ct = default) => GetGraphViewAsync(rootId, depth, ct);
}
