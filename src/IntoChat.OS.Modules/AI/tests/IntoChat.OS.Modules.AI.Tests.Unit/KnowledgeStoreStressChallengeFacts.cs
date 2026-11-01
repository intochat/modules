using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DigitalBrain;
using DigitalBrain.Invocation;
using DigitalBrain.State;
using DigitalBrain.Testing;
using IntoChat.OS.Modules.AI.Memory;
using Xunit;

namespace IntoChat.OS.Modules.AI.Tests.Unit;

public sealed class KnowledgeStoreStressChallengeFacts
{
    private static (TestOS os, IKnowledgeStore store) CreateNeuronHarness(string id = "test-store")
    {
        var os = TestOS.Local();
        os.Register<KnowledgeStoreNeuron>();
        var store = os.Get<IKnowledgeStore>(new NeuronId(new BrainId("local"), "ai.knowledge-store", id));
        return (os, store);
    }

    [Fact]
    public async Task StoreAsync_OverloadAmbiguity_ThrowsBindingExceptionOnNeuronProxy()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, store) = CreateNeuronHarness("overload-test");
        await using (os)
        {
            var node = new KnowledgeNode("ambiguous-node", "Ambiguous", "concept", new Dictionary<string, string>());
            var edge = new KnowledgeEdge("ambiguous-node", "ambiguous-node", "self");

            // Calling RecordNodeAsync and RecordEdgeAsync on a NeuronProxy resolves unambiguously without BindingException
            await store.RecordNodeAsync(node, ct);
            await store.RecordEdgeAsync(edge, ct);

            var retrieved = await store.GetAsync("ambiguous-node", ct);
            Assert.NotNull(retrieved);
            Assert.Equal("ambiguous-node", retrieved.Id);

            var edges = await store.QueryEdgesAsync("ambiguous-node", EdgeDirection.Both, ct);
            Assert.Single(edges);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CyclicGraph_SelfLoop_TerminatesWithoutDuplicationOrLoop(bool useNeuron)
    {
        var ct = TestContext.Current.CancellationToken;
        IKnowledgeStore store;
        TestOS? os = null;

        if (useNeuron)
        {
            (os, store) = CreateNeuronHarness("cyclic-self");
        }
        else
        {
            store = new InMemoryKnowledgeStore();
        }

        try
        {
            var nodeA = new KnowledgeNode("node-a", "Node A", "concept", new Dictionary<string, string>());
            if (useNeuron)
            {
                await store.RecordNodeAsync(nodeA, ct);
                await store.RecordEdgeAsync(new KnowledgeEdge("node-a", "node-a", "self_referential"), ct);
            }
            else
            {
                await store.RecordNodeAsync(nodeA, ct);
                await store.RecordEdgeAsync(new KnowledgeEdge("node-a", "node-a", "self_referential"), ct);
            }

            // Traverse with depth 1, 2, 5, and large depth (10)
            foreach (var depth in new[] { 1, 2, 5, 10 })
            {
                var view = await store.GetGraphViewAsync("node-a", depth, ct);
                Assert.NotNull(view);
                Assert.Single(view.Nodes);
                Assert.Equal("node-a", view.Nodes[0].Id);
                Assert.Single(view.Edges);
                Assert.Equal("node-a", view.Edges[0].SourceId);
                Assert.Equal("node-a", view.Edges[0].TargetId);
            }
        }
        finally
        {
            if (os is not null)
            {
                await os.DisposeAsync();
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CyclicGraph_MutualCycle_TerminatesWithExactNodesAndEdges(bool useNeuron)
    {
        var ct = TestContext.Current.CancellationToken;
        IKnowledgeStore store;
        TestOS? os = null;

        if (useNeuron)
        {
            (os, store) = CreateNeuronHarness("cyclic-mutual");
        }
        else
        {
            store = new InMemoryKnowledgeStore();
        }

        try
        {
            var nodeA = new KnowledgeNode("node-a", "Node A", "entity", new Dictionary<string, string>());
            var nodeB = new KnowledgeNode("node-b", "Node B", "entity", new Dictionary<string, string>());
            if (useNeuron)
            {
                await store.RecordNodeAsync(nodeA, ct);
                await store.RecordNodeAsync(nodeB, ct);
                await store.RecordEdgeAsync(new KnowledgeEdge("node-a", "node-b", "calls"), ct);
                await store.RecordEdgeAsync(new KnowledgeEdge("node-b", "node-a", "replies"), ct);
            }
            else
            {
                await store.RecordNodeAsync(nodeA, ct);
                await store.RecordNodeAsync(nodeB, ct);
                await store.RecordEdgeAsync(new KnowledgeEdge("node-a", "node-b", "calls"), ct);
                await store.RecordEdgeAsync(new KnowledgeEdge("node-b", "node-a", "replies"), ct);
            }

            // BFS with deep traversal (5)
            var view = await store.GetGraphViewAsync("node-a", depth: 5, ct);
            Assert.NotNull(view);
            Assert.Equal(2, view.Nodes.Count);
            Assert.Contains(view.Nodes, n => n.Id == "node-a");
            Assert.Contains(view.Nodes, n => n.Id == "node-b");

            // Edges must not duplicate
            Assert.Equal(2, view.Edges.Count);
            Assert.Contains(view.Edges, e => e.SourceId == "node-a" && e.TargetId == "node-b" && e.Relation == "calls");
            Assert.Contains(view.Edges, e => e.SourceId == "node-b" && e.TargetId == "node-a" && e.Relation == "replies");
        }
        finally
        {
            if (os is not null)
            {
                await os.DisposeAsync();
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CyclicGraph_TriangleAndDenseMesh_DoesNotCauseStackOverflowOrInfiniteLoop(bool useNeuron)
    {
        var ct = TestContext.Current.CancellationToken;
        IKnowledgeStore store;
        TestOS? os = null;

        if (useNeuron)
        {
            (os, store) = CreateNeuronHarness("cyclic-mesh");
        }
        else
        {
            store = new InMemoryKnowledgeStore();
        }

        try
        {
            const int nodeCount = 10;
            for (var i = 0; i < nodeCount; i++)
            {
                var n = new KnowledgeNode($"mesh-{i}", $"Mesh Node {i}", "node", new Dictionary<string, string>());
                if (useNeuron)
                {
                    await store.RecordNodeAsync(n, ct);
                }
                else
                {
                    await store.RecordNodeAsync(n, ct);
                }
            }

            // Create dense mesh where each node connects to every other node (90 directed edges)
            for (var i = 0; i < nodeCount; i++)
            {
                for (var j = 0; j < nodeCount; j++)
                {
                    if (i != j)
                    {
                        var e = new KnowledgeEdge($"mesh-{i}", $"mesh-{j}", "connected_to");
                        if (useNeuron)
                        {
                            await store.RecordEdgeAsync(e, ct);
                        }
                        else
                        {
                            await store.RecordEdgeAsync(e, ct);
                        }
                    }
                }
            }

            // Traverse from mesh-0 with max depth (5)
            var view = await store.GetGraphViewAsync("mesh-0", depth: 5, ct);
            Assert.NotNull(view);
            Assert.Equal(nodeCount, view.Nodes.Count);
            Assert.Equal(90, view.Edges.Count);
        }
        finally
        {
            if (os is not null)
            {
                await os.DisposeAsync();
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CascadingDeletion_RemovesAllIncidentEdgesBothWays(bool useNeuron)
    {
        var ct = TestContext.Current.CancellationToken;
        IKnowledgeStore store;
        TestOS? os = null;

        if (useNeuron)
        {
            (os, store) = CreateNeuronHarness("cascade-del");
        }
        else
        {
            store = new InMemoryKnowledgeStore();
        }

        try
        {
            var center = new KnowledgeNode("center", "Center Hub", "hub", new Dictionary<string, string>());
            if (useNeuron)
            {
                await store.RecordNodeAsync(center, ct);
                await store.RecordEdgeAsync(new KnowledgeEdge("center", "center", "loop"), ct);
            }
            else
            {
                await store.RecordNodeAsync(center, ct);
                await store.RecordEdgeAsync(new KnowledgeEdge("center", "center", "loop"), ct);
            }

            // 5 satellites
            for (var i = 0; i < 5; i++)
            {
                var sat = new KnowledgeNode($"sat-{i}", $"Satellite {i}", "satellite", new Dictionary<string, string>());
                var outEdge = new KnowledgeEdge("center", $"sat-{i}", "controls");
                var inEdge = new KnowledgeEdge($"sat-{i}", "center", "reports_to");

                if (useNeuron)
                {
                    await store.RecordNodeAsync(sat, ct);
                    await store.RecordEdgeAsync(outEdge, ct);
                    await store.RecordEdgeAsync(inEdge, ct);
                }
                else
                {
                    await store.RecordNodeAsync(sat, ct);
                    await store.RecordEdgeAsync(outEdge, ct);
                    await store.RecordEdgeAsync(inEdge, ct);
                }
            }

            // Before deletion: center has 1 self + 5 outgoing + 5 incoming = 11 edges
            var beforeEdges = await store.QueryEdgesAsync("center", EdgeDirection.Both, ct);
            Assert.Equal(11, beforeEdges.Count);

            // Delete center
            await store.DeleteNodeAsync("center", ct);

            // Center is deleted
            Assert.Null(await store.GetAsync("center", ct));

            // All satellites now have 0 edges connected to center
            for (var i = 0; i < 5; i++)
            {
                var satEdges = await store.QueryEdgesAsync($"sat-{i}", EdgeDirection.Both, ct);
                Assert.Empty(satEdges);
            }

            // Graph view has no remaining edges
            var view = await store.GetGraphViewAsync(null, depth: 2, ct);
            Assert.Equal(5, view.Nodes.Count);
            Assert.Empty(view.Edges);
        }
        finally
        {
            if (os is not null)
            {
                await os.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task BoundaryConditions_EmptyStoreAndDisconnectedComponents()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryKnowledgeStore();

        // 1. Empty graph view
        var emptyView = await store.GetGraphViewAsync("non-existent", depth: 2, ct);
        Assert.Empty(emptyView.Nodes);
        Assert.Empty(emptyView.Edges);

        var nullRootView = await store.GetGraphViewAsync(null, depth: 2, ct);
        Assert.Empty(nullRootView.Nodes);
        Assert.Empty(nullRootView.Edges);

        // 2. Empty edges query
        Assert.Empty(await store.QueryEdgesAsync("non-existent", EdgeDirection.Both, ct));

        // 3. Empty semantic search
        Assert.Empty(await store.SemanticSearchAsync("", limit: 10, ct));
        Assert.Empty(await store.SemanticSearchAsync("query", limit: 0, ct));
        Assert.Empty(await store.SemanticSearchAsync("query", limit: -5, ct));

        // 4. Safe deletions on non-existent elements
        await store.DeleteNodeAsync("non-existent", ct);
        await store.DeleteEdgeAsync("src", "dst", "rel", ct);

        // 5. Disconnected components
        await store.RecordNodeAsync(new KnowledgeNode("c1-a", "Comp1 A", "type", new Dictionary<string, string>()), ct);
        await store.RecordNodeAsync(new KnowledgeNode("c1-b", "Comp1 B", "type", new Dictionary<string, string>()), ct);
        await store.RecordEdgeAsync(new KnowledgeEdge("c1-a", "c1-b", "links"), ct);

        await store.RecordNodeAsync(new KnowledgeNode("c2-x", "Comp2 X", "type", new Dictionary<string, string>()), ct);
        await store.RecordNodeAsync(new KnowledgeNode("c2-y", "Comp2 Y", "type", new Dictionary<string, string>()), ct);
        await store.RecordEdgeAsync(new KnowledgeEdge("c2-x", "c2-y", "links"), ct);

        // Querying from c1-a must NOT return c2 components
        var c1View = await store.GetGraphViewAsync("c1-a", depth: 5, ct);
        Assert.Equal(2, c1View.Nodes.Count);
        Assert.Contains(c1View.Nodes, n => n.Id == "c1-a");
        Assert.Contains(c1View.Nodes, n => n.Id == "c1-b");
        Assert.DoesNotContain(c1View.Nodes, n => n.Id.StartsWith("c2", StringComparison.Ordinal));
        Assert.Single(c1View.Edges);

        // Querying with null root returns all components
        var fullView = await store.GetGraphViewAsync(null, depth: 2, ct);
        Assert.Equal(4, fullView.Nodes.Count);
        Assert.Equal(2, fullView.Edges.Count);
    }

    [Fact]
    public void KnowledgeStoreState_ConformsToPersistedStateRules_PureArrays()
    {
        var stateType = typeof(KnowledgeStoreState);
        Assert.NotNull(stateType.GetCustomAttribute<GenerateSerializerAttribute>());
        Assert.NotNull(stateType.GetCustomAttribute<AliasAttribute>());

        // Check property types on KnowledgeStoreState
        var properties = stateType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            var pType = prop.PropertyType;
            if (pType.IsClass && pType != typeof(string))
            {
                Assert.True(pType.IsArray, $"Property {prop.Name} in KnowledgeStoreState must be an array, but is {pType.Name}");
            }
        }

        // Check KnowledgeNodeRecord
        var nodeRecordType = typeof(KnowledgeNodeRecord);
        Assert.NotNull(nodeRecordType.GetCustomAttribute<GenerateSerializerAttribute>());
        foreach (var prop in nodeRecordType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var pType = prop.PropertyType;
            if (pType.IsClass && pType != typeof(string))
            {
                Assert.True(pType.IsArray, $"Property {prop.Name} in KnowledgeNodeRecord must be an array, but is {pType.Name}");
            }
        }

        // Check KnowledgeEdgeRecord
        var edgeRecordType = typeof(KnowledgeEdgeRecord);
        Assert.NotNull(edgeRecordType.GetCustomAttribute<GenerateSerializerAttribute>());
        foreach (var prop in edgeRecordType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var pType = prop.PropertyType;
            if (pType.IsClass && pType != typeof(string))
            {
                Assert.True(pType.IsArray, $"Property {prop.Name} in KnowledgeEdgeRecord must be an array, but is {pType.Name}");
            }
        }

        // Check KnowledgePropertyRecord
        var propRecordType = typeof(KnowledgePropertyRecord);
        Assert.NotNull(propRecordType.GetCustomAttribute<GenerateSerializerAttribute>());
    }

    [Fact]
    public async Task SemanticSearch_WithCyclicMesh_AssignsFiniteScoresWithoutExploding()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemoryKnowledgeStore();

        // 3 nodes in full mutual cycle
        var nodeA = new KnowledgeNode("node-a", "Quantum Encryption Protocol", "security", new Dictionary<string, string> { ["algo"] = "lattice" }, Importance: 5.0);
        var nodeB = new KnowledgeNode("node-b", "Key Distribution Center", "security", new Dictionary<string, string> { ["protocol"] = "bb84" }, Importance: 5.0);
        var nodeC = new KnowledgeNode("node-c", "Entropy Source Hardware", "crypto", new Dictionary<string, string> { ["hw"] = "trng" }, Importance: 5.0);

        await store.RecordNodeAsync(nodeA, ct);
        await store.RecordNodeAsync(nodeB, ct);
        await store.RecordNodeAsync(nodeC, ct);

        // Cyclic edges
        await store.RecordEdgeAsync(new KnowledgeEdge("node-a", "node-b", "routes_keys", Weight: 2.0), ct);
        await store.RecordEdgeAsync(new KnowledgeEdge("node-b", "node-a", "confirms_routes", Weight: 2.0), ct);
        await store.RecordEdgeAsync(new KnowledgeEdge("node-b", "node-c", "harvests_entropy", Weight: 2.0), ct);
        await store.RecordEdgeAsync(new KnowledgeEdge("node-c", "node-a", "seeds_lattice", Weight: 2.0), ct);

        var hits = await store.SemanticSearchAsync("Quantum Encryption", limit: 5, ct);

        Assert.NotEmpty(hits);
        Assert.Contains(hits, h => h.Node.Id == "node-a");
        Assert.Contains(hits, h => h.Node.Id == "node-b");

        // All scores must be finite, non-NaN, and positive
        foreach (var hit in hits)
        {
            Assert.False(double.IsNaN(hit.RelevanceScore));
            Assert.False(double.IsInfinity(hit.RelevanceScore));
            Assert.True(hit.RelevanceScore > 0);
        }
    }
}
