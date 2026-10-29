using System.Text.Json;
using IntoChat.OS.Framework.Compute;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime.Signals;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

public sealed class BrainRunsStateFacts
{
    [Fact]
    public void SamplesRetainTheLatest31SuccessesAndUseTheLowerMedian()
    {
        var facts = Enumerable.Range(1, 40).Select(number => Fact(number, number)).ToArray();
        var state = new BrainRunsState().Apply(facts);
        var statistics = Assert.Single(state.Statistics);
        Assert.Equal(Enumerable.Range(10, 31).Select(value => (long)value), statistics.Samples.Select(sample => sample.Microseconds));
        Assert.Equal(25, statistics.P50Microseconds);
        var pair = new BrainRunsState().Apply([Fact(1, 10), Fact(2, 100)]);
        Assert.Equal(10, Assert.Single(pair.Statistics).P50Microseconds);
        Assert.Same(state, state.Apply(facts.TakeLast(10).ToArray()));
    }

    [Fact]
    public void HashChangesResetSamplesAndFailuresRemainInHistoryOnly()
    {
        var state = new BrainRunsState().Apply([Fact(1, 10), Fact(2, 20, outcome: RunOutcome.Skipped),
            Fact(3, 0), Fact(4, 50, outcome: RunOutcome.Failed), Fact(5, 0, outcome: RunOutcome.Dropped)]);
        Assert.Equal(4, state.Recent.Length);
        Assert.Single(Assert.Single(state.Statistics).Samples);
        state = state.Apply([Fact(6, 90, hash: "new")]);
        var statistics = Assert.Single(state.Statistics);
        Assert.Equal("new", statistics.Hash);
        Assert.Equal(90, Assert.Single(statistics.Samples).Microseconds);
        Assert.Equal(5, state.Recent.Length);
    }

    [Fact]
    public void PerOwnerRetentionPreserves200AfterExcludingABusyOwner()
    {
        var state = new BrainRunsState().Apply(Enumerable.Range(1, 205).Select(number => Fact(number, number)).ToArray());
        state = state.Apply(Enumerable.Range(1, 250).Select(number => Fact(number, number, owner: "brain/apps/brainview")).ToArray());
        Assert.Equal(400, state.Recent.Length);
        var visible = state.Recent.Where(entry => entry.Owner != "brain/apps/brainview").TakeLast(200).ToArray();
        Assert.Equal(200, visible.Length);
        Assert.Equal(6, visible[0].DurationMicroseconds);
        Assert.Equal(205, visible[^1].DurationMicroseconds);
        var reloaded = JsonSerializer.Deserialize<BrainRunsState>(JsonSerializer.Serialize(state))!;
        Assert.Equal(state.Recent, reloaded.Recent);
        Assert.Equal(state.Revision, reloaded.Revision);
        Assert.Equal(state.Sequence, reloaded.Sequence);
        Assert.Same(reloaded, reloaded.Apply([Fact(250, 250, owner: "brain/apps/brainview")]));
    }

    [Fact]
    public void EntriesKeepTheirOwnTraceAndLegacyCompatibleMarks()
    {
        var first = new Trace { TraceId = "first", PayerAccountId = "account", BrainId = "brain", Origin = TraceContext.OriginOf("brain", "brain/edge") };
        var second = first with { TraceId = "second" };
        var state = new BrainRunsState().Apply([Fact(1, 10) with { Trace = first }, Fact(2, 20) with { Trace = second }]);
        var owed = new BrainRunsState { Unannounced = ["brain/apps/app/synapses/a.cs"] }.Apply([Fact(1, 10)]);
        Assert.Equal(["brain/apps/app/synapses/a.cs"], owed.Unannounced);
        Assert.Equal(["first", "second"], state.Recent.Select(entry => entry.TraceId));
        Assert.Equal("brain/apps/app/synapses/a.cs|source|Changed|1|Succeeded", state.Recent[0].Mark);
        Assert.Equal("source|Changed|1", state.Statistics[0].Samples[0].Mark);
    }

    private static RunFact Fact(long watermark, long duration, string hash = "hash", RunOutcome outcome = RunOutcome.Succeeded,
        string owner = "brain/apps/app") => RunFact.Of(new BehaviorRan("brain/runtime", owner, owner + "/synapses/a.cs", hash,
            "source", "Changed", watermark, outcome, 1, duration, 0, null), null);
}
