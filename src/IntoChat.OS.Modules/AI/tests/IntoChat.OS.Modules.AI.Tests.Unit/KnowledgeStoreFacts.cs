using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IntoChat.OS.Modules.AI.Memory;

namespace IntoChat.OS.Modules.AI.Tests.Unit;

public sealed class KnowledgeStoreFacts
{
    private readonly InMemoryKnowledgeStore _store = new();

    [Fact]
    public async Task StoreAndGetNodePreservesProperties()
    {
        var ct = TestContext.Current.CancellationToken;
        var node = new KnowledgeNode(
            "node-1",
            "Order Processing Service",
            "microservice",
            new Dictionary<string, string> { ["language"] = "csharp", ["tier"] = "backend" },
            Importance: 8.5);

        await _store.RecordNodeAsync(node, ct);
        var retrieved = await _store.GetAsync("node-1", ct);

        Assert.NotNull(retrieved);
        Assert.Equal("node-1", retrieved.Id);
        Assert.Equal("Order Processing Service", retrieved.Label);
        Assert.Equal("microservice", retrieved.Type);
        Assert.Equal("csharp", retrieved.Properties["language"]);
        Assert.Equal(8.5, retrieved.Importance);
    }

    [Fact]
    public async Task DeleteNodeCascadesEdgeRemoval()
    {
        var ct = TestContext.Current.CancellationToken;
        var nodeA = new KnowledgeNode("node-a", "Node A", "component", new Dictionary<string, string>());
        var nodeB = new KnowledgeNode("node-b", "Node B", "component", new Dictionary<string, string>());
        var edge = new KnowledgeEdge("node-a", "node-b", "depends_on", 1.0);

        await _store.RecordNodeAsync(nodeA, ct);
        await _store.RecordNodeAsync(nodeB, ct);
        await _store.RecordEdgeAsync(edge, ct);

        var beforeEdges = await _store.QueryEdgesAsync("node-a", EdgeDirection.Both, ct);
        Assert.Single(beforeEdges);

        await _store.DeleteNodeAsync("node-a", ct);

        Assert.Null(await _store.GetAsync("node-a", ct));
        var afterEdges = await _store.QueryEdgesAsync("node-b", EdgeDirection.Both, ct);
        Assert.Empty(afterEdges);
    }

    [Fact]
    public async Task GetGraphViewPerformsBfsTraversal()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = new KnowledgeNode("root", "Root System", "root", new Dictionary<string, string>());
        var child1 = new KnowledgeNode("child-1", "Child One", "service", new Dictionary<string, string>());
        var child2 = new KnowledgeNode("child-2", "Child Two", "service", new Dictionary<string, string>());
        var leaf = new KnowledgeNode("leaf", "Leaf Worker", "worker", new Dictionary<string, string>());

        await _store.RecordNodeAsync(root, ct);
        await _store.RecordNodeAsync(child1, ct);
        await _store.RecordNodeAsync(child2, ct);
        await _store.RecordNodeAsync(leaf, ct);

        await _store.RecordEdgeAsync(new KnowledgeEdge("root", "child-1", "parent_of"), ct);
        await _store.RecordEdgeAsync(new KnowledgeEdge("root", "child-2", "parent_of"), ct);
        await _store.RecordEdgeAsync(new KnowledgeEdge("child-1", "leaf", "dispatches_to"), ct);

        // Depth 1: should include root and children, but NOT leaf
        var viewDepth1 = await _store.GetGraphViewAsync("root", depth: 1, ct: ct);
        Assert.Equal(3, viewDepth1.Nodes.Count);
        Assert.DoesNotContain(viewDepth1.Nodes, n => n.Id == "leaf");

        // Depth 2: should include leaf
        var viewDepth2 = await _store.GetGraphViewAsync("root", depth: 2, ct: ct);
        Assert.Equal(4, viewDepth2.Nodes.Count);
        Assert.Contains(viewDepth2.Nodes, n => n.Id == "leaf");
    }

    [Fact]
    public async Task SemanticSearchPropagatesScoreViaSpreadingActivation()
    {
        var ct = TestContext.Current.CancellationToken;
        var authNode = new KnowledgeNode("auth", "Authentication Gateway", "security", new Dictionary<string, string> { ["protocol"] = "oauth" }, Importance: 5.0);
        var tokenNode = new KnowledgeNode("token", "Token Issuer", "crypto", new Dictionary<string, string> { ["key"] = "rsa" }, Importance: 5.0);
        var unlinkedNode = new KnowledgeNode("printer", "Network Printer", "hardware", new Dictionary<string, string>(), Importance: 1.0);

        await _store.RecordNodeAsync(authNode, ct);
        await _store.RecordNodeAsync(tokenNode, ct);
        await _store.RecordNodeAsync(unlinkedNode, ct);

        // Link auth to token with high weight
        await _store.RecordEdgeAsync(new KnowledgeEdge("auth", "token", "issues_for", Weight: 2.0), ct);

        // Query for "Authentication" matches authNode directly
        var hits = await _store.SemanticSearchAsync("Authentication", limit: 3, ct: ct);

        Assert.NotEmpty(hits);
        Assert.Equal("auth", hits[0].Node.Id);

        // tokenNode should receive spreading activation score and be ranked second
        var tokenHit = Assert.Single(hits, h => h.Node.Id == "token");
        Assert.True(tokenHit.RelevanceScore > 0);
    }
}
