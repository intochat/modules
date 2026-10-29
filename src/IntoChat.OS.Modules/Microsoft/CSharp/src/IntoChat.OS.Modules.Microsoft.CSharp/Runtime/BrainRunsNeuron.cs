using System.Text.Json.Serialization;
using IntoChat.OS.Framework;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime.Signals;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// How the brain runtime hands a turn's run facts to its brain's runs neuron: one call per turn. Not a neuron
// contract (module-internal), so no edge, script or MCP caller can resolve it; BrainRunsGuard admits only that brain's runtime.
[DigitalBrain.Contracts.NeuronAlias("microsoft.csharp.runs-recorder"), DigitalBrain.Contracts.DefaultNeuronType(BrainRunsNeuron.GrainTypeName)]
internal interface IBrainRunsRecorder
{
    // announce is false while another behavior is still running. The facts are kept either way;
    // the changed signal waits until a record that finishes the last in-flight behavior.
    Task Record(RunFact[] facts, bool announce);

    // Publishes the changed signal for facts already recorded while later behavior was still starting.
    Task Announce();
}

// A run fact (a started or a finished run) and the trace of the turn or batch it belongs to. Plain signals carry no trace of their
// own (06-08): the trace is kept on the run entry the snapshot records.
internal sealed record RunFact(BehaviorStarted? Started, BehaviorRan? Ran, Trace? Trace)
{
    // A factory, not a second constructor: the fact crosses the runtime's JSON envelope, which binds one constructor.
    public static RunFact Of(DigitalBrain.Signal fact, Trace? trace)
        => fact is BehaviorStarted or BehaviorRan ? new(fact as BehaviorStarted, fact as BehaviorRan, trace)
            : throw new ArgumentException("A run fact is a BehaviorStarted or a BehaviorRan.", nameof(fact));

    [JsonIgnore] public DigitalBrain.Signal Fact => (DigitalBrain.Signal?)Ran ?? Started!;
}

// Facts and their read model share the storage write. Apps read snapshots instead of copying every run.
// A plain neuron (06-08) at "{brain}/runs"; every fact is saved, so the brain's log holds it for a subscriber's catch-up.
[DigitalBrain.Neuron(GrainTypeName)]
internal sealed class BrainRunsNeuron : DigitalBrain.Neuron<BrainRunsState>, IBrainRuns, IBrainRunsRecorder
{
    internal const string GrainTypeName = "microsoft.csharp.runs";

    private BrainRunsState Snapshot => State;

    public Task<BrainRunsSnapshot> Read(string? excludedOwner = null)
    {
        var prefix = Id.Brain.Value + "/apps/";
        if (excludedOwner is not null && (!excludedOwner.StartsWith(prefix, StringComparison.Ordinal)
            || excludedOwner.Length == prefix.Length || excludedOwner[prefix.Length..].Contains('/')))
        { throw new ArgumentException("Exclude an app owner in this brain.", nameof(excludedOwner)); }
        return Task.FromResult(new BrainRunsSnapshot(Snapshot.Revision,
            [.. Snapshot.Recent.Where(item => item.Owner != excludedOwner).TakeLast(200)], Snapshot.Statistics));
    }

    public Task Record(RunFact[] facts, bool announce = true)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var next = Snapshot.Apply(facts);
        DigitalBrain.Signal[] signals = [.. facts.Select(fact => fact.Fact)];
        var behaviors = Behaviors(facts);
        var announced = Snapshot.Unannounced.Concat(behaviors).Distinct(StringComparer.Ordinal).ToArray();
        if (!announce)
        {
            var held = Same(announced, Snapshot.Unannounced) ? next : next with { Unannounced = announced };
            return signals.Length == 0 && ReferenceEquals(held, Snapshot) ? Task.CompletedTask : Save(held, signals.Length > 0 ? signals : [Changed(held)]);
        }
        var cleared = Snapshot.Unannounced.Length == 0 ? next : next with { Unannounced = [] };
        if (announced.Length == 0)
        { return signals.Length == 0 && ReferenceEquals(cleared, Snapshot) ? Task.CompletedTask : Save(cleared, signals.Length > 0 ? signals : [Changed(cleared)]); }
        var revision = ReferenceEquals(next, Snapshot) ? Snapshot.Revision : next.Revision;
        return Save(cleared, [.. signals, new BrainRunsChanged(revision, announced)]);
    }

    public Task Announce()
    {
        if (Snapshot.Unannounced.Length == 0) { return Task.CompletedTask; }
        var behaviors = Snapshot.Unannounced;
        return Save(Snapshot with { Unannounced = [] }, new BrainRunsChanged(Snapshot.Revision, behaviors));
    }

    // A state change with no fact to carry it (only the unannounced set moved) still says what changed.
    private static BrainRunsChanged Changed(BrainRunsState state) => new(state.Revision, []);

    private static bool Same(string[] left, string[] right)
        => left.Length == right.Length && left.AsSpan().SequenceEqual(right);

    private static string[] Behaviors(RunFact[] facts)
        => [.. facts.Select(fact => fact.Ran).OfType<BehaviorRan>().Where(ran => ran.Outcome != RunOutcome.Skipped)
            .Select(ran => ran.Behavior).Distinct(StringComparer.Ordinal)];
}

// Nobody publishes runs as someone else: the neuron the kernel runs a Record for must be the runtime of the runs neuron's own brain.
// Both are plain neurons (06-08): the call leaves from the runtime's host grain and reaches the runs neuron's host as one
// RuntimeInvocation, read from the ids Orleans sets on the message (never from the call's body).
internal sealed class BrainRunsGuard : IIncomingGrainCallFilter
{
    private static readonly string Recorder = DigitalBrain.Invocation.NeuronProxy.ContractName(typeof(IBrainRunsRecorder));

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        if (context.Request.GetArgumentCount() > 0 && context.Request.GetArgument(0) is DigitalBrain.Invocation.RuntimeInvocation call
            && string.Equals(call.Contract, Recorder, StringComparison.Ordinal) && !Admits(context.SourceId, context.TargetId))
        { throw new UnauthorizedAccessException("Only a brain's own runtime records its synapse runs."); }
        await context.Invoke();
    }

    internal static bool Admits(GrainId? source, GrainId target)
        => source is { } caller && caller.Type.ToString()!.StartsWith("neuron.host", StringComparison.Ordinal)
            && DigitalBrain.NeuronId.TryParse(caller.Key.ToString()!, out var runtime) && runtime.Type == BrainRuntimeNeuron.GrainTypeName
            && DigitalBrain.NeuronId.TryParse(target.Key.ToString()!, out var runs) && runtime.Brain == runs.Brain;
}
