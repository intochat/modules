using System;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.AI.Memory;

[GenerateSerializer, Alias("ai.knowledge-store-state")]
[ExportableState]
public sealed record KnowledgeStoreState
{
    [Id(0)] public long Revision { get; init; }
    [Id(1)] public KnowledgeNodeRecord[] Nodes { get; init; } = [];
    [Id(2)] public KnowledgeEdgeRecord[] Edges { get; init; } = [];

    public KnowledgeStoreState() : this(0, [], []) { }
    public KnowledgeStoreState(long revision, KnowledgeNodeRecord[] nodes, KnowledgeEdgeRecord[] edges)
    {
        Revision = revision;
        Nodes = nodes;
        Edges = edges;
    }
}

[GenerateSerializer, Alias("ai.knowledge-node-record")]
public sealed record KnowledgeNodeRecord
{
    [Id(0)] public string Id { get; init; } = "";
    [Id(1)] public string Label { get; init; } = "";
    [Id(2)] public string Type { get; init; } = "";
    [Id(3)] public KnowledgePropertyRecord[] Properties { get; init; } = [];
    [Id(4)] public float[]? Vector { get; init; }
    [Id(5)] public double Importance { get; init; } = 1.0;
    [Id(6)] public DateTimeOffset CreatedAt { get; init; }
    [Id(7)] public DateTimeOffset UpdatedAt { get; init; }
    // Provenance, flattened: persisted records hold scalars and arrays only. Null Writer means written before provenance existed.
    [Id(8)] public string? Writer { get; init; }
    [Id(9)] public string? Caller { get; init; }
    [Id(10)] public DateTimeOffset StampedAt { get; init; }
}

[GenerateSerializer, Alias("ai.knowledge-property-record")]
public sealed record KnowledgePropertyRecord
{
    [Id(0)] public string Key { get; init; } = "";
    [Id(1)] public string Value { get; init; } = "";
}

[GenerateSerializer, Alias("ai.knowledge-edge-record")]
public sealed record KnowledgeEdgeRecord
{
    [Id(0)] public string SourceId { get; init; } = "";
    [Id(1)] public string TargetId { get; init; } = "";
    [Id(2)] public string Relation { get; init; } = "";
    [Id(3)] public double Weight { get; init; } = 1.0;
    [Id(4)] public KnowledgePropertyRecord[] Metadata { get; init; } = [];
    [Id(5)] public DateTimeOffset CreatedAt { get; init; }
    [Id(6)] public string? Writer { get; init; }
    [Id(7)] public string? Caller { get; init; }
    [Id(8)] public DateTimeOffset StampedAt { get; init; }
}
