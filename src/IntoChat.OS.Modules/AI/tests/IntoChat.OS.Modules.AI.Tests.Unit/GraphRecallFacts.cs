using DigitalBrain.Testing;
using IntoChat.OS.Modules.AI.Memory;

namespace IntoChat.OS.Modules.AI.Tests.Unit;

public sealed class GraphRecallFacts(AiFixture fixture) : IClassFixture<AiFixture>
{
    public static TheoryData<NeuronRuntimeKind> Kinds => AiFixture.Kinds;

    [Theory, MemberData(nameof(Kinds))]
    public async Task AFactTwoRelationsAwayIsRecalledWithItsPath(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var store = runtime.Get<IKnowledgeStore>(runtime.Id("ai.knowledge-store", "recall-" + kind));
        await store.RecordNodeAsync(new KnowledgeNode("pr-42", "PR 42 failed CI", "fact", new Dictionary<string, string>(), Importance: 5), ct);
        await store.RecordNodeAsync(new KnowledgeNode("repo-main", "GitHub main repository", "entity", new Dictionary<string, string>()), ct);
        await store.RecordNodeAsync(new KnowledgeNode("owner-ada", "Ada owns the repository", "fact", new Dictionary<string, string>()), ct);
        await store.RecordNodeAsync(new KnowledgeNode("lunch", "Lunch is at noon", "fact", new Dictionary<string, string>()), ct);
        await store.RecordEdgeAsync(new KnowledgeEdge("pr-42", "repo-main", "in"), ct);
        await store.RecordEdgeAsync(new KnowledgeEdge("repo-main", "owner-ada", "owned_by"), ct);

        var hits = await store.SemanticSearchAsync("failed CI", 5, ct);

        Assert.Equal("pr-42", hits[0].Node.Id);
        Assert.NotNull(hits[0].Node.Source);
        var reached = Assert.Single(hits, hit => hit.Node.Id == "owner-ada");
        Assert.Equal(["pr-42", "repo-main", "owner-ada"], reached.AssociativePath);
        Assert.DoesNotContain(hits, hit => hit.Node.Id == "lunch");
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task MemoryFactsReachTheStoreOnceAndRecallMergesBoth(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var memory = runtime.Get<IMemory>(runtime.Id("ai.memory", "sync-" + kind));
        var store = runtime.Get<IKnowledgeStore>(runtime.Id("ai.knowledge-store", KnowledgeGraphSource.StoreKey));

        var fact = await memory.RememberFact("deploy", "Rollout needs a confirmation", 7);
        var copied = await store.GetAsync(fact.Id, ct);

        Assert.NotNull(copied);
        Assert.Equal("fact", copied.Type);
        Assert.Single((await memory.Read()).Facts);
        var hits = await memory.Recall("confirmation rollout", 5);
        Assert.Single(hits, hit => hit.Id == fact.Id);
    }
}
