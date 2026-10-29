using DigitalBrain.Capabilities;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IntoChat.OS.Client.Runtime;
using IntoChat.OS.Client.Runtime.V1;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Contracts.Compute;
using IntoChat.OS.Framework.Compute;
using IntoChat.OS.Modules.Microsoft.CSharp.Assemblies;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
using IntoChat.OS.Modules.Microsoft.CSharp.Files;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime.Signals;
using IntoChat.OS.Modules.Microsoft.CSharp.Sessions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using DigitalBrain.Runtime.Orleans;
using DigitalBrain.Runtime.Orleans.Hosting;
using DigitalBrain.Runtime.Orleans.Signalling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// A brain's programs, their durable subscriptions and the one sandbox session that runs them. Every
// change goes through the same rules: a session is wanted while anything is assigned; the host is told
// the difference over its stream; signals buffer until the host acknowledges them. The reminder is the
// backstop that relaunches a lost session, not the detector. What the synapses' runs do becomes facts that
// the brain's runs neuron publishes, one hand-off per turn after the turn's write.
// Every script call and every live signal of the brain passes through here, so a turn holds only state
// changes: the pure reads interleave (their contracts say so), a source that spoke is read from its log in a one-way turn of its
// own, compiling happens before an Assign takes its turn, and pricing a run happens after the ack's.
// A plain neuron (06-08) at "{brain}/runtime": every source is a plain neuron, heard through the program's session on the session
// synapse neuron (06-07); the observer path is gone.
[DigitalBrain.Neuron(GrainTypeName)]
internal sealed class BrainRuntimeNeuron(
    SessionRunner runner,
    SessionTokens tokens,
    SessionStreams streams,
    DeferredRuntimeStarts deferredStarts,
    IBehaviorAssemblies assemblies,
    CSharpScriptCheck compiler,
    CompiledTypes types,
    TimeProvider time,
    IOptions<CSharpOptions> options,
    IOptions<CSharpOptions> deployment,
    ILogger<BrainRuntimeNeuron> logger,
    RuntimeDirectory directory)
    : DigitalBrain.Neuron<BrainRuntimeState>, IBrainRuntime, IBrainRuntimeEdge, IBrainPrograms, IBrainProgramChanges, IBrainRuntimeArrivals, IBrainRuntimeContinuations, IBrainRuntimeSession
{
    private BrainRuntimeState Snapshot => State;
    private IServiceProvider ServiceProvider => Context.Services;
    // Plain sessions are durable synapses; the renewal re-subscribes (Subscribe is idempotent) and so restores a session that expired.
    private const string RenewalTimer = "subscription-renewal";
    private static readonly TimeSpan SubscriptionRenewal = TimeSpan.FromMinutes(1);
    private bool _renewing;
    internal const string GrainTypeName = "microsoft.csharp.runtime";
    // How many of a plain app's requests seed a new subscription: the window the app grain retained (64).
    private const int RetainedRequests = 64;
    internal const string ReconcileReminder = "reconcile";
    // Orleans reminders fire at most once a minute.
    private static readonly TimeSpan ReconcilePeriod = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan SilenceLimit = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan AssemblyUrlLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LogsCap = TimeSpan.FromSeconds(10);
    // Rotated well inside SessionTokens.Lifetime, so a reminder that fires late still beats expiry.
    private static readonly TimeSpan TokenRotation = TimeSpan.FromMinutes(40);
    // Development sandboxes reach the brain on the Docker host.
    internal const string DockerHost = "host.docker.internal";

    // Transitional (06-07, R4-15): plain sources (PlainSources) are heard through one session on the session synapse neuron per
    // program, not an observer; the session loop asks this runtime to read each source that spoke from its BrainLog.
    private readonly HashSet<string> _plain = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CancellationTokenSource> _plainLoops = new(StringComparer.Ordinal);
    // Last status the sandbox reported per behavior id. Memory only: a fresh activation shows Starting until the host speaks.
    private readonly Dictionary<string, (string Status, string? Error)> _reported = new(StringComparer.Ordinal);
    // The trace each pushed batch carried, per (subscription, watermark), so its ack prices the CPU it cost under that trace.
    private readonly Dictionary<(string Subscription, long Watermark), BatchTrace> _batchTraces = new();
    // Batches that reached a live stream and are not acknowledged yet, per (subscription, watermark): each started a run.
    private readonly HashSet<(string Subscription, long Watermark)> _started = [];
    // What each behavior's runs did since this activation. Memory only, like _reported.
    private readonly Dictionary<string, RunCounts> _runs = new(StringComparer.Ordinal);
    // This turn's run facts: handed to the brain's runs neuron in one call, once the turn's write succeeded.
    private readonly List<RunFact> _facts = [];
    private static readonly ActivitySource Telemetry = new("IntoChat.OS.Modules.Microsoft.CSharp.Runtime");
    private ICostMeter? _meter;
    // The tail of this activation's sandbox calls; each runs after the one before it, whatever its outcome.
    private Task _sandboxCalls = Task.CompletedTask;
    // The tail of this activation's pricing of acknowledged runs, priced one after another off the acks' turns.
    private Task _pricing = Task.CompletedTask;
    // Recording runs off-turn: publication may call this runtime back. Idle includes its durable completion.
    private Task _recording = Task.CompletedTask;
    private readonly List<RunFact> _pendingRecords = [];
    private bool _announceRuns, _announceOwed, _recordingFailed;
    // Nudge turns still reading their source: Inspect interleaves, so a read in flight must not look idle.
    private int _reading;
    // Tokens for work a neuron detached from its caller. The turn stays open until that work signals.
    private readonly HashSet<string> _continuations = new(StringComparer.Ordinal);
    private const int MaximumContinuations = 64;
    // Programs compiled and stored while the runtime went on, each waiting for its turn, by ticket.
    private readonly Dictionary<long, CompiledProgram> _compiled = [];
    // Per owner, the ticket of its program's last change (an Assign applied, or a Retire): an Assign that started
    // before that change and finished compiling after it is not applied.
    private readonly Dictionary<string, long> _changed = new(StringComparer.Ordinal);
    private long _tickets;
    // Per owner, the program a reassign replaced and what went with it, kept until the sandbox has the new build running. If the
    // sandbox cannot drain the old run it keeps it and reports the update failed; the old program is then put back.
    private readonly Dictionary<string, Superseded> _superseded = new(StringComparer.Ordinal);
    // Callers of Settled, waiting for an owner's program to report.
    private readonly List<(string Owner, TaskCompletionSource Reported)> _settling = [];
    // Assign requests in flight and the last one applied per owner, by RequestKey: a repeat joins or answers them.
    private readonly Dictionary<string, Task<ProgramView>> _assigning = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Request, ProgramView View)> _applied = new(StringComparer.Ordinal);

    private string RuntimeId => $"{Id.Brain.Value}/{Id.Key}";
    private string Brain => Id.Brain.Value;

    // The host composer may not have wired a meter yet; a brain without one simply does not price CPU.
    private ICostMeter? Meter => _meter ??= ServiceProvider.GetService<ICostMeter>();

    // A subscription to any brain's runs neuron is a reaction to run facts, and its own runs publish none: a
    // reaction to its own brain's runs would feed itself, and two brains reacting to each other's would feed each other.
    private static bool IsRunFacts(string neuron) => PlainSources.TryParse(neuron, out var source) && source.Type == BrainRunsNeuron.GrainTypeName;

    // The brain's runs neuron, "{brain}/runs".
    private IBrainRunsRecorder Runs => Get<IBrainRunsRecorder>(new DigitalBrain.NeuronId(Id.Brain, BrainRunsNeuron.GrainTypeName, "runs"));

    // A one-way call to this neuron: it completes when sent, and the work takes a turn of its own.
    private Task Self<T>(Func<T, Task> call) where T : class => call(Get<T>(Id));

    private ISessionSynapses PlainSession(string session)
        => SiloNeurons.Get<ISessionSynapses>(ServiceProvider, SessionSynapses.For(Id.Brain, session));

    private IBrainJournalWriter Journal => ServiceProvider.GetRequiredService<IBrainJournalWriter>();

    // The traces still held for pushed batches; a settled batch lets go of its trace.
    internal int TracedBatches => _batchTraces.Count;

    public Task<BrainRuntimeSnapshot> Inspect(CancellationToken cancellationToken = default)
        => Task.FromResult(new BrainRuntimeSnapshot(Snapshot.Session?.Status ?? SessionStatus.None, Snapshot.Session?.Id,
            Snapshot.Session?.Fingerprint, Snapshot.Session?.LastHeartbeat,
            [.. Snapshot.Programs.SelectMany(program => View(program).Behaviors)], IsReady(),
            IsReady() && _pricing.IsCompleted && _recording.IsCompletedSuccessfully && !_recordingFailed));

    protected override Task OnActivate(CancellationToken cancellationToken) => ResumeAsync();

    protected override async Task OnDeactivate(DigitalBrain.DeactivationCause cause, CancellationToken cancellationToken)
    {
        // Graceful retirement keeps this activation alive until all queued history has committed.
        // A process crash or canceled shutdown still needs durable replay; this lane is not an outbox.
        // Plain sources' sessions outlive the activation: their synapses are durable, and the next activation reads the BrainLog.
        foreach (var stop in _plainLoops.Values) { stop.Cancel(); }
        _plainLoops.Clear();
        await _recording.WaitAsync(cancellationToken);
        if (_pendingRecords.Count > 0 || _announceOwed)
        {
            BeginRecording();
            await _recording.WaitAsync(cancellationToken);
        }
    }

    // Initialization is the supported persistent script's single outer On/React wait, plus its
    // drained durable signals. A continuation is work a neuron detached that has not signaled yet.
    // Independent parallel initializers and downstream projections are not covered.
    private bool IsReady() => Snapshot.Session?.Status == SessionStatus.Connected
        && Snapshot.Programs.Any(program => program.Behaviors.Length > 0)
        && _assigning.Count == 0 && _compiled.Count == 0 && _reading == 0 && _started.Count == 0
        && _continuations.Count == 0
        && Snapshot.Subscriptions.All(subscription => subscription.Pending.Length == 0)
        && Snapshot.Programs.All(program => program.Behaviors.All(behavior =>
            Snapshot.Initialized.GetValueOrDefault(behavior.Id(program.Owner)) == AssignmentRevision(program, behavior)
            && Snapshot.Subscriptions.Any(subscription => subscription.Behavior == behavior.Id(program.Owner))));

    public async Task Touch(CancellationToken cancellationToken = default)
    {
        if (Snapshot.Programs.Length > 0) { await EnsureSessionAsync(); }
    }

    public async Task Hold(CancellationToken cancellationToken = default)
    {
        var start = await PrepareSessionAsync(restart: false) ?? throw NotListening();
        await _sandboxCalls;
        await runner.Ensure(start, cancellationToken);
    }

    public Task Acquire(string token)
    {
        RequireContinuation(token);
        if (_continuations.Contains(token)) { return Task.CompletedTask; }
        if (_continuations.Count >= MaximumContinuations)
        { throw new InvalidOperationException("This brain has too many outstanding continuations."); }
        _continuations.Add(token);
        return Task.CompletedTask;
    }

    public Task Release(string token)
    {
        RequireContinuation(token);
        if (_continuations.Remove(token)) { Flush(); }
        return Task.CompletedTask;
    }

    private void RequireContinuation(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
        { throw new ArgumentException("A continuation token is required.", nameof(token)); }
        if (CallerContextStamper.TryGet(out var caller) && !string.Equals(caller.BrainId, Brain, StringComparison.Ordinal))
        { throw new UnauthorizedAccessException("A continuation belongs to its own brain."); }
    }

    // Interleaved (its contract says so): compiling is seconds of CPU, off this runtime's scheduler, and storing
    // the assemblies is I/O; neither touches the runtime's state. The compiled program then takes a turn of its own.
    // Idempotent by request: the same owner, sources and settings asked again while that program compiles joins
    // it, and asked again once it is applied (and still current) answers it, so a caller whose call timed out asks
    // again instead of compiling the program twice.
    public Task<ProgramView> Assign(string owner, Dictionary<string, string> sources, Dictionary<string, string> settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(settings);
        // The caller is part of the request: the same program assigned by another principal is applied again.
        return Assigned(owner, RequestKey(owner, sources, settings, CallerContextStamper.TryGet(out var caller) ? caller : null), settings,
            () => Compile(sources));
    }

    // The prebuilt twin of Assign: the program was compiled ahead of time and arrives as assemblies. Nothing is compiled; each
    // assembly must hash to the name it is stored and loaded under, which is the check the sandbox repeats on download. A mismatch
    // refuses the whole program before anything is stored or applied. Request idempotency, storing and applying are Assign's.
    public Task<ProgramView> AssignPrebuilt(string owner, Dictionary<string, PrebuiltBehavior> behaviors, Dictionary<string, string> settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentNullException.ThrowIfNull(behaviors);
        ArgumentNullException.ThrowIfNull(settings);
        if (behaviors.Values.Any(behavior => behavior?.Assembly is null || behavior.Symbols is null || behavior.Hash is null))
        { throw new ArgumentException("A prebuilt behavior carries its assembly, its symbols and its hash.", nameof(behaviors)); }
        var named = behaviors.ToDictionary(pair => pair.Key, pair => "prebuilt:" + pair.Value.Hash, StringComparer.Ordinal);
        return Assigned(owner, RequestKey(owner, named, settings, CallerContextStamper.TryGet(out var caller) ? caller : null), settings,
            () => Verified(behaviors));
    }

    private readonly Dictionary<string, MemoryStream> _staging = new(StringComparer.Ordinal);

    public async Task Stage(string hash, int offset, byte[] chunk, bool last)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        ArgumentNullException.ThrowIfNull(chunk);
        if (offset == 0) { _staging[hash] = new(); }
        if (!_staging.TryGetValue(hash, out var staged) || staged.Length != offset)
        { _staging.Remove(hash); throw new InvalidOperationException($"The chunk at {offset} does not continue the assembly staged for {hash}; stage it again from the start."); }
        staged.Write(chunk);
        if (!last) { return; }
        _staging.Remove(hash);
        var bytes = staged.ToArray();
        if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), hash, StringComparison.Ordinal))
        { throw new InvalidDataException($"The staged assembly does not hash to {hash}; discarded."); }
        if (!await assemblies.Exists(hash, CancellationToken.None)) { await assemblies.Put(new(bytes, [], hash), CancellationToken.None); }
    }

    private Task<ProgramView> Assigned(string owner, string request, Dictionary<string, string> settings, Func<Task<IReadOnlyDictionary<string, CompiledBehavior>>> produce)
    {
        if (_assigning.TryGetValue(request, out var running)) { return running; }
        if (_applied.TryGetValue(owner, out var applied) && applied.Request == request
            && Snapshot.Programs.FirstOrDefault(p => p.Owner == owner)?.Hash == applied.View.Hash)
        {
            // Asked again, the current program is the owner's latest change: an older Assign still compiling
            // (one whose caller gave up and reverted to this) is not applied after it.
            _changed[owner] = ++_tickets;
            return Task.FromResult(applied.View);
        }
        var assigning = AssignOnce(owner, request, settings, produce);
        if (!assigning.IsCompleted) { _assigning[request] = assigning; }
        return assigning;
    }

    private static string RequestKey(string owner, Dictionary<string, string> sources, Dictionary<string, string> settings, CapabilityCaller? caller)
    {
        var text = new StringBuilder(owner).Append('\0').Append(caller?.ToString()).Append('\0');
        foreach (var (path, source) in sources.OrderBy(file => file.Key, StringComparer.Ordinal)) { text.Append(path).Append('\0').Append(source).Append('\0'); }
        text.Append('\u0001');
        foreach (var (name, value) in settings.OrderBy(setting => setting.Key, StringComparer.Ordinal)) { text.Append(name).Append('\0').Append(value).Append('\0'); }
        return owner + "\n" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    private async Task<ProgramView> AssignOnce(string owner, string request, Dictionary<string, string> settings, Func<Task<IReadOnlyDictionary<string, CompiledBehavior>>> produce)
    {
        try
        {
            var view = await Stored(owner, settings, produce);
            _applied[owner] = (request, view);
            return view;
        }
        finally { _assigning.Remove(request); }
    }

    private async Task<IReadOnlyDictionary<string, CompiledBehavior>> Compile(Dictionary<string, string> sources)
    {
        var compiled = await Task.Run(() => compiler.Compile(sources));
        if (!compiled.Success)
        {
            throw new InvalidOperationException("The program does not compile: "
                + string.Join("; ", compiled.Errors.Select(error => $"{error.File} line {error.Line} {error.Id}: {error.Message}")));
        }
        return compiled.Assemblies;
    }

    private async Task<IReadOnlyDictionary<string, CompiledBehavior>> Verified(Dictionary<string, PrebuiltBehavior> behaviors)
    {
        foreach (var (path, behavior) in behaviors)
        {
            // Staged earlier: the bytes were checked against this very hash when the last chunk arrived.
            if (behavior.Assembly.Length == 0)
            {
                if (!await assemblies.Exists(behavior.Hash, CancellationToken.None))
                { throw new InvalidOperationException($"The assembly for {path} ({behavior.Hash}) was not staged; stage it before assigning."); }
                continue;
            }
            if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(behavior.Assembly)), behavior.Hash, StringComparison.Ordinal))
            { throw new InvalidDataException($"The prebuilt assembly for {path} does not hash to {behavior.Hash}; refusing to assign it."); }
        }
        return behaviors.ToDictionary(pair => pair.Key, pair => new CompiledBehavior(pair.Value.Assembly, pair.Value.Symbols, pair.Value.Hash), StringComparer.Ordinal);
    }

    // The one tail both ways in share: the ticket is taken before the program is produced, so a later change outdates it; the
    // assemblies are stored; and the program takes its turn.
    private async Task<ProgramView> Stored(string owner, Dictionary<string, string> settings, Func<Task<IReadOnlyDictionary<string, CompiledBehavior>>> produce)
    {
        var ticket = ++_tickets;
        var caller = CallerContextStamper.TryGet(out var stamped) ? stamped : null;
        var produced = await produce();
        foreach (var behavior in produced.Values)
        { if (!await assemblies.Exists(behavior.Hash, CancellationToken.None)) { await assemblies.Put(behavior, CancellationToken.None); } }
        BehaviorAssembly[] behaviors = [.. produced.OrderBy(file => file.Key, StringComparer.Ordinal)
            .Select(file => new BehaviorAssembly(file.Key, file.Value.Hash))];
        _compiled[ticket] = new(owner, behaviors, new(settings, StringComparer.Ordinal), caller);
        // This request holds no turn, so the call to itself waits only for the turn in progress, never for this one.
        try { return await Get<IBrainProgramChanges>(Id).Apply(ticket); }
        finally { _compiled.Remove(ticket); }
    }

    // A synapse that is gone, or whose assembly changed, is not the synapse its subscriptions were declared by:
    // they go with it, and a changed synapse's new assembly declares its own when it runs. A program that now
    // speaks for another principal is judged again by every source it watches.
    public async Task<ProgramView> Apply(long ticket)
    {
        if (!_compiled.TryGetValue(ticket, out var compiled))
        { throw new InvalidOperationException("No program this runtime compiled waits under that ticket."); }
        var owner = compiled.Owner;
        if (_changed.GetValueOrDefault(owner) > ticket)
        { throw new InvalidOperationException($"The program of {owner} changed again while this one compiled; it was not applied."); }
        _changed[owner] = ticket;
        var behaviors = compiled.Behaviors;
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", behaviors.Select(b => b.Path + ":" + b.Hash)))));
        var previous = Snapshot.Programs.FirstOrDefault(p => p.Owner == owner);
        var program = ProgramAssignment.Of(owner, hash, compiled.Settings, compiled.Caller ?? previous?.Principal, behaviors);
        var replaced = previous?.Behaviors.Except(behaviors).Select(b => b.Id(owner)).ToArray() ?? [];
        // With a sandbox connected the old build may keep running, so what replacing it removes is kept until the new one runs.
        if (previous is not null && replaced.Length > 0 && Snapshot.Session is { Status: SessionStatus.Connected })
        {
            _superseded[owner] = _superseded.TryGetValue(owner, out var open) ? open with { NewHash = hash }
                : new(previous, [.. Snapshot.Subscriptions.Where(s => previous.Behaviors.Any(b => b.Id(owner) == s.Behavior))],
                    new Dictionary<string, string>(Snapshot.AssignmentRevisions, StringComparer.Ordinal),
                    new Dictionary<string, string>(Snapshot.Initialized, StringComparer.Ordinal), hash);
        }
        else { _superseded.Remove(owner); }
        var removed = replaced.Except(behaviors.Select(b => b.Id(owner)), StringComparer.Ordinal).ToArray();
        var revisions = new Dictionary<string, string>(Snapshot.AssignmentRevisions, StringComparer.Ordinal);
        var initialized = new Dictionary<string, string>(Snapshot.Initialized, StringComparer.Ordinal);
        foreach (var gone in removed) { revisions.Remove(gone); initialized.Remove(gone); }
        foreach (var behavior in behaviors)
        {
            var id = behavior.Id(owner);
            var unchanged = previous is not null && previous.Principal == program.Principal
                && previous.Settings.Count == program.Settings.Count
                && previous.Settings.All(pair => program.Settings.TryGetValue(pair.Key, out var value) && value == pair.Value)
                && previous.Behaviors.Contains(behavior);
            if (unchanged && revisions.ContainsKey(id)) { continue; }
            revisions[id] = Guid.NewGuid().ToString("N");
            initialized.Remove(id);
        }
        await Save(Snapshot with
        {
            Programs = [.. Snapshot.Programs.Where(p => p.Owner != owner), program],
            Subscriptions = [.. Snapshot.Subscriptions.Where(s => !replaced.Contains(s.Behavior, StringComparer.Ordinal))],
            AssignmentRevisions = revisions,
            Initialized = initialized,
        }, Changed());
        foreach (var gone in replaced) { Forget(gone); }
        if (replaced.Length > 0 || (previous is not null && previous.Principal != program.Principal)) { await WatchSourcesAsync(); }
        // More apps can install after the first deferred session was recorded, while HTTP still has no address.
        if (Snapshot.Session is null || EdgeUrl() is null) { await EnsureSessionAsync(); }
        else
        {
            foreach (var gone in removed) { streams.TryPush(Snapshot.Session.Id, RetireEvent.Kind, new RetireEvent(gone)); }
            foreach (var behavior in behaviors) { streams.TryPush(Snapshot.Session.Id, AssignmentEvent.Kind, await AssignmentFor(program, behavior)); }
        }
        return View(program);
    }

    private sealed record Superseded(ProgramAssignment Program, Subscription[] Subscriptions, Dictionary<string, string> Revisions,
        Dictionary<string, string> Initialized, string NewHash);

    private sealed record CompiledProgram(string Owner, BehaviorAssembly[] Behaviors, Dictionary<string, string> Settings, CapabilityCaller? Caller);

    public async Task Retire(string owner)
    {
        // The owner's latest change: an Assign still compiling from before it is not applied after it.
        _changed[owner] = ++_tickets;
        if (Snapshot.Programs.FirstOrDefault(p => p.Owner == owner) is not { } program) { return; }
        var ids = program.Behaviors.Select(b => b.Id(owner)).ToArray();
        await Save(Snapshot with
        {
            Programs = [.. Snapshot.Programs.Where(p => p.Owner != owner)],
            Subscriptions = [.. Snapshot.Subscriptions.Where(s => !ids.Contains(s.Behavior, StringComparer.Ordinal))],
            Initialized = Snapshot.Initialized.Where(pair => !ids.Contains(pair.Key, StringComparer.Ordinal)).ToDictionary(StringComparer.Ordinal),
            AssignmentRevisions = Snapshot.AssignmentRevisions.Where(pair => !ids.Contains(pair.Key, StringComparer.Ordinal)).ToDictionary(StringComparer.Ordinal),
        }, Changed());
        foreach (var id in ids) { Forget(id); }
        await WatchSourcesAsync();
        if (Snapshot.Session is { } session) { foreach (var id in ids) { streams.TryPush(session.Id, RetireEvent.Kind, new RetireEvent(id)); } }
        if (Snapshot.Programs.Length == 0) { await EndSessionAsync(); }
    }

    // What this activation remembers of a behavior that is no longer assigned, and of its batches in flight.
    private void Forget(string behavior)
    {
        _reported.Remove(behavior);
        _runs.Remove(behavior);
        var live = Snapshot.Subscriptions.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        _started.RemoveWhere(key => !live.Contains(key.Subscription));
        foreach (var key in _batchTraces.Keys.Where(key => !live.Contains(key.Subscription)).ToArray()) { _batchTraces.Remove(key); }
    }

    // The behavior id's owner is a prefix; the longest matching owner wins.
    private ProgramAssignment? Find(string behavior)
        => Snapshot.Programs.Where(p => p.Behaviors.Any(b => b.Id(p.Owner) == behavior)).OrderByDescending(p => p.Owner.Length).FirstOrDefault();

    // The program a behavior belongs to and its own assembly's hash; null for a behavior no longer assigned.
    private (string Owner, string Hash)? Synapse(string behavior)
        => Find(behavior) is { } program && program.Behaviors.FirstOrDefault(b => b.Id(program.Owner) == behavior) is { } assembly
            ? (program.Owner, assembly.Hash)
            : null;

    Task<ProgramView?> IBrainPrograms.Describe(string owner)
        => Task.FromResult(Snapshot.Programs.FirstOrDefault(p => p.Owner == owner) is { } program ? View(program) : null);

    public Task<BehaviorView?> Describe(string behavior)
    {
        var program = Find(behavior);
        return Task.FromResult(program is null ? null : View(program).Behaviors.FirstOrDefault(view => view.Id == behavior));
    }

    public Task<string> Logs(string behavior, int tail)
        => Snapshot.Session is { Status: SessionStatus.Connected } session
            ? streams.RequestLogsAsync(session.Id, behavior, Math.Clamp(tail, 1, 5000), LogsCap)
            : Task.FromResult("");

    public Task<SessionAuthorization> Authorize(string session)
        => Task.FromResult(new SessionAuthorization(Speaks(session), Brain));

    public Task<BehaviorIdentity?> Identity(string session, string behavior)
    {
        RequireSpeaking(session);
        return Task.FromResult(Find(behavior) is { } program ? new BehaviorIdentity(program.Principal, program.Owner) : null);
    }

    // A host built from other contracts must not load behaviors compiled against these: it is refused,
    // stopped, and the reason stays in the session record.
    public async Task Connected(string session, string fingerprint)
    {
        RequireSpeaking(session);
        if (!ContractsFingerprint.Covers(fingerprint, ContractsFingerprint.Of(types.Assemblies)))
        {
            logger.LogError("Sandbox session {Session} of brain {Brain} carries other contracts than this silo; refused. Rebuild the sandbox image from this build.", session, Brain);
            await Save(Snapshot with { Session = Snapshot.Session! with { Status = SessionStatus.Refused, Fingerprint = fingerprint } }, Changed());
            OffTurn(runner.Stop, Brain, "stop");
            return;
        }
        await Save(Snapshot with { Session = Snapshot.Session! with { Status = SessionStatus.Connected, Fingerprint = fingerprint, LastHeartbeat = time.GetUtcNow() } }, Changed());
    }

    public Task Heartbeat(string session)
    {
        RequireSpeaking(session);
        return Save(Snapshot with { Session = Snapshot.Session! with { LastHeartbeat = time.GetUtcNow() } }, Changed());
    }

    public Task<string> Subscribed(string session, string behavior, string neuron, string signal)
        => Subscribed(session, behavior, neuron, signal, selector: null);

    public async Task<string> Subscribed(string session, string behavior, string neuron, string signal, string? selector)
    {
        selector = string.IsNullOrEmpty(selector) ? null : selector;
        RequireSpeaking(session);
        if (Find(behavior) is null) { throw new ArgumentException($"{behavior} is not assigned to this brain.", nameof(behavior)); }
        if (!PlainSources.Is(neuron)) { throw new ArgumentException($"'{neuron}' is not a neuron id such as plain:brain/time.timer/tea.", nameof(neuron)); }
        // Subscribing to itself would self-feed on every save.
        if (neuron == PlainSources.Of(Id)) { throw new ArgumentException("A behavior cannot subscribe to its brain runtime.", nameof(neuron)); }
        ArgumentException.ThrowIfNullOrWhiteSpace(signal);
        _facts.Clear();
        var existing = Snapshot.Subscriptions.FirstOrDefault(s => s.Behavior == behavior && s.Neuron == neuron && s.Signal == signal);
        if (existing is null)
        {
            // A new subscription starts at its source's current seq; what came before is the retained seed's to give.
            PlainSources.TryParse(neuron, out var plain);
            var position = (await Journal.ReadSince(plain.Brain.Value, plain.ToString(), 0, 1, CancellationToken.None)).Last;
            existing = new(Guid.NewGuid().ToString("N"), behavior, neuron, signal, [], 0, 0, position, selector);
            // A source that retains the signal seeds the new subscription with the backlog the synapse
            // missed. Watch before seeding: anything published between the seed and the watch arrives
            // through the watch itself, and a duplicate is at-least-once's to bear; nothing is lost.
            await Save(Snapshot with { Subscriptions = [.. Snapshot.Subscriptions, existing] }, Changed());
            try { await WatchSourcesAsync(required: existing); }
            catch (Exception error) when (IsRefusal(error)) { await RefuseAsync(existing); throw; }
            var seeded = await SeedRetainedAsync(neuron, signal, selector);
            if (seeded.Length > 0)
            {
                var current = Snapshot.Subscriptions.First(s => s.Id == existing.Id);
                var known = seeded.ToHashSet(StringComparer.Ordinal);
                existing = current with { Pending = [.. seeded, .. current.Pending.Where(json => !known.Contains(json))] };
                await Save(Snapshot with { Subscriptions = [.. Snapshot.Subscriptions.Where(s => s.Id != existing.Id), existing] }, Changed());
            }
        }
        else
        {
            if (!string.Equals(existing.Selector, selector, StringComparison.Ordinal))
            {
                // The row stays the subscription: a second row would keep delivering what this selector now excludes.
                existing = existing with { Selector = selector };
                await Save(Snapshot with { Subscriptions = [.. Snapshot.Subscriptions.Select(item => item.Id == existing.Id ? existing : item)] }, Changed());
            }
            // Persistence can succeed before source registration fails. A retry is ready only
            // after the source has accepted the current selection, including the saved addition.
            try { await PlainWatchAsync(neuron, required: existing); }
            catch (Exception error) when (IsRefusal(error)) { await RefuseAsync(existing); throw; }
            // The log after the watermark comes first; a source whose part is leased elsewhere fails the subscribe here.
            await CatchUpAsync(neuron, push: false);
            existing = Snapshot.Subscriptions.First(s => s.Id == existing.Id);
        }
        // Both a retained initial seed and a reloaded behavior's backlog must reach an already
        // connected session without another arrival. The client buffers batches before its reader joins.
        if (existing.Pending.Length > 0 && Snapshot.Session is { Status: SessionStatus.Connected }) { Push(session, existing); }
        logger.LogInformation("BehaviorSubscribed {Behavior} {Neuron} {Signal}", behavior, neuron, signal);
        Flush();
        return existing.Id;
    }

    public Task Acknowledge(string session, string subscription, long watermark)
    {
        RequireSpeaking(session);
        return AcknowledgeAsync([new AcknowledgeReport(subscription, watermark)]);
    }

    public Task AcknowledgeMany(string session, AcknowledgeReport[] acknowledgements)
    {
        RequireSpeaking(session);
        ArgumentNullException.ThrowIfNull(acknowledgements);
        if (acknowledgements.Length is 0 or > RuntimeProtocol.MaximumAcknowledgements
            || acknowledgements.Any(ack => ack is null || string.IsNullOrWhiteSpace(ack.Subscription) || ack.Watermark < 0
                || ack.CpuMicroseconds < 0 || ack.WallMicroseconds < 0 || ack.Batches < 0))
        { throw new ArgumentException("An acknowledgement batch needs 1 to 256 valid subscription watermarks.", nameof(acknowledgements)); }
        return AcknowledgeAsync([.. acknowledgements.GroupBy(ack => ack.Subscription, StringComparer.Ordinal)
            .Select(group => new AcknowledgeReport(group.Key, group.Max(ack => ack.Watermark), group.Sum(ack => ack.CpuMicroseconds),
                group.Sum(ack => ack.WallMicroseconds), group.Sum(ack => ack.Batches), group.All(ack => ack.Skipped)))]);
    }

    // One write trims every acknowledged subscription. A report that moved Delivered is a finished run; a
    // replayed one is nothing. Either way the batches it covers, at or below its watermark, are no longer in flight.
    private async Task AcknowledgeAsync(AcknowledgeReport[] reports)
    {
        _facts.Clear();
        var byId = reports.ToDictionary(report => report.Subscription, StringComparer.Ordinal);
        var advanced = new List<(Subscription Before, Subscription After, AcknowledgeReport Report)>();
        var next = Snapshot.Subscriptions.Select(subscription =>
        {
            if (!byId.TryGetValue(subscription.Id, out var report)) { return subscription; }
            var trimmed = subscription.Acknowledge(report.Watermark);
            if (trimmed.Delivered != subscription.Delivered) { advanced.Add((subscription, trimmed, report)); }
            return trimmed;
        }).ToArray();
        if (advanced.Count > 0) { await Save(Snapshot with { Subscriptions = next }, Changed()); }
        var traces = reports.ToDictionary(report => report.Subscription, report => Settle(report.Subscription, report.Watermark), StringComparer.Ordinal);
        foreach (var (before, after, report) in advanced)
        {
            if (IsRunFacts(before.Neuron) || Synapse(before.Behavior) is not { } synapse) { continue; }
            Ran(new BehaviorRan(RuntimeId, synapse.Owner, before.Behavior, synapse.Hash, before.Neuron, before.Signal, after.Delivered,
                report.Skipped ? RunOutcome.Skipped : RunOutcome.Succeeded, report.Batches, report.WallMicroseconds, report.CpuMicroseconds, null), traces[report.Subscription]);
        }
        Flush();
        // A replayed ack prices nothing again. Metering observes the run: it is priced after this turn, so a slow
        // ledger never holds the runtime, and a meter that fails does not fail the ack.
        PricedRun[] priced = [.. advanced.Where(run => !run.Report.Skipped && run.Report.CpuMicroseconds > 0).Select(run =>
        {
            var program = Find(run.Before.Behavior);
            return new PricedRun(run.Report.Subscription, run.Report.Watermark, run.Report.CpuMicroseconds,
                run.Before.Behavior, program?.Owner, run.After.Delivered, traces[run.Report.Subscription]?.Trace,
                program is null ? null : new BehaviorIdentity(program.Principal, program.Owner).Caller);
        })];
        if (priced.Length > 0 && Meter is { } meter) { _pricing = PriceAsync(_pricing, meter, priced); }
    }

    private sealed record PricedRun(string Subscription, long Watermark, long CpuMicroseconds, string Behavior, string? App, long Delivered, Trace? Trace, CapabilityCaller? Caller);

    // A pushed batch's trace and the traceparent it carried: the run's span sits under the batch's own span.
    private sealed record BatchTrace(Trace Trace, string TraceParent);

    // Runs on this activation's scheduler between turns and reads nothing of the runtime's state.
    private async Task PriceAsync(Task previous, ICostMeter meter, PricedRun[] runs)
    {
        await previous;
        foreach (var run in runs)
        {
            try
            {
                // An untraced batch still belongs to the program's saved principal. Scope each run
                // separately: one acknowledgement can settle programs with different callers.
                await AsProgram(run.Caller, async () =>
                {
                    using var entered = run.Trace is { } batch ? TraceContext.Enter(batch) : null;
                    await meter.RecordAsync(CostKind.Script, "cpu-ms", run.CpuMicroseconds / 1000m, "ms", Funding.Local,
                        $"{run.Subscription}:{run.Watermark}", CancellationToken.None, new CostSubject(Synapse: run.Behavior, App: run.App));
                });
            }
            catch (Exception error)
            { logger.LogWarning(error, "Brain {Brain} could not price {Behavior}'s run through {Watermark}.", Brain, run.Behavior, run.Delivered); }
        }
    }

    // The batches of a subscription at or below an acknowledged watermark are settled: none is in flight any
    // more. The trace of the latest of them is the run's.
    private BatchTrace? Settle(string subscription, long watermark)
    {
        BatchTrace? latest = null;
        var at = long.MinValue;
        foreach (var key in _batchTraces.Keys.Where(key => key.Subscription == subscription && key.Watermark <= watermark).ToArray())
        {
            if (key.Watermark > at) { (at, latest) = (key.Watermark, _batchTraces[key]); }
            _batchTraces.Remove(key);
        }
        _started.RemoveWhere(key => key.Subscription == subscription && key.Watermark <= watermark);
        return latest;
    }

    // The status the sandbox last reported for an assigned behavior. An exit is remembered, never disarmed:
    // the sandbox restarts an exited behavior itself while its assignment stands.
    public async Task Reported(string session, BehaviorStatusReport status)
    {
        RequireSpeaking(session);
        ArgumentNullException.ThrowIfNull(status);
        _facts.Clear();
        if (Find(status.Behavior) is { } program && program.Behaviors.FirstOrDefault(b => b.Id(program.Owner) == status.Behavior && b.Hash == status.Hash) is { } assembly)
        {
            if (status.AssignmentRevision is { } reportedRevision && reportedRevision != AssignmentRevision(program, assembly)) { return; }
            if (status.Status == "Initialized")
            {
                var revision = AssignmentRevision(program, assembly);
                if (status.AssignmentRevision == revision)
                {
                    var initialized = new Dictionary<string, string>(Snapshot.Initialized, StringComparer.Ordinal) { [status.Behavior] = revision };
                    await Save(Snapshot with { Initialized = initialized }, Changed());
                }
                return;
            }
            if (status.Status == "UpdateFailed" && _superseded.TryGetValue(program.Owner, out var superseded) && superseded.NewHash == program.Hash)
            {
                await RestoreAsync(program, superseded, status);
                NotifySettled(program.Owner);
                Flush();
                return;
            }
            if (status.Status is "Exited" or "Stopped")
            {
                var initialized = new Dictionary<string, string>(Snapshot.Initialized, StringComparer.Ordinal);
                initialized.Remove(status.Behavior);
                await Save(Snapshot with { Initialized = initialized }, Changed());
            }
            _reported[status.Behavior] = (status.Status, status.Error);
            if (status is { Status: "Exited", ExitCode: not (null or 0) }) { Failed(program.Owner, assembly.Hash, status); }
            // The new build is running: what its predecessor left behind is no longer needed.
            if (status.Status == "Running" && program.Behaviors.All(b => _reported.GetValueOrDefault(b.Id(program.Owner)).Status == "Running")) { _superseded.Remove(program.Owner); }
            NotifySettled(program.Owner);
        }
        Flush();
    }

    // The sandbox kept the old run: the program that run belongs to is the assigned one again, with its subscriptions (so the signals
    // it is owed are read from the sources' logs again), and the sandbox is told so every behavior returns to it. The failure stays
    // reported for the behavior that could not be updated.
    private async Task RestoreAsync(ProgramAssignment current, Superseded old, BehaviorStatusReport failure)
    {
        _superseded.Remove(current.Owner);
        var owner = current.Owner;
        var oldIds = old.Program.Behaviors.Select(b => b.Id(owner)).ToHashSet(StringComparer.Ordinal);
        var newOnly = current.Behaviors.Select(b => b.Id(owner)).Where(id => !oldIds.Contains(id)).ToArray();
        var revisions = new Dictionary<string, string>(Snapshot.AssignmentRevisions, StringComparer.Ordinal);
        var initialized = new Dictionary<string, string>(Snapshot.Initialized, StringComparer.Ordinal);
        foreach (var id in newOnly) { revisions.Remove(id); initialized.Remove(id); }
        foreach (var id in oldIds)
        {
            if (old.Revisions.TryGetValue(id, out var revision)) { revisions[id] = revision; } else { revisions.Remove(id); }
            if (old.Initialized.TryGetValue(id, out var ready)) { initialized[id] = ready; } else { initialized.Remove(id); }
        }
        var kept = Snapshot.Subscriptions.Where(s => !oldIds.Contains(s.Behavior) && !newOnly.Contains(s.Behavior, StringComparer.Ordinal)).ToArray();
        await Save(Snapshot with
        {
            Programs = [.. Snapshot.Programs.Where(p => p.Owner != owner), old.Program],
            Subscriptions = [.. kept, .. old.Subscriptions],
            AssignmentRevisions = revisions,
            Initialized = initialized,
        }, Changed());
        foreach (var id in newOnly.Concat(oldIds)) { _reported.Remove(id); }
        _reported[failure.Behavior] = (failure.Status, failure.Error);
        logger.LogWarning("The sandbox could not replace {Behavior}; the previous program {Hash} of {Owner} stays assigned: {Error}", failure.Behavior, old.Program.Hash, owner, failure.Error);
        await WatchSourcesAsync();
        if (Snapshot.Session is { } session)
        {
            foreach (var id in newOnly) { streams.TryPush(session.Id, RetireEvent.Kind, new RetireEvent(id)); }
            foreach (var behavior in old.Program.Behaviors) { streams.TryPush(session.Id, AssignmentEvent.Kind, await AssignmentFor(old.Program, behavior)); }
        }
        _ = Self<IBrainRuntimeArrivals>(arrivals => arrivals.CatchUp());
    }

    // Interleaved: waits for each behavior of the owner's current program to report (running, failed to update, or exited), or the
    // timeout; answers the program as it then stands. The assigner of a program reads from it whether the program it asked for runs.
    public async Task<ProgramView?> Settled(string owner, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        // With no sandbox connected nothing can report; the program starts when one connects.
        if (Snapshot.Session is not { Status: SessionStatus.Connected }) { return Snapshot.Programs.FirstOrDefault(p => p.Owner == owner) is { } waiting ? View(waiting) : null; }
        using var limit = new CancellationTokenSource(timeout, time);
        while (!limit.IsCancellationRequested)
        {
            if (Snapshot.Programs.FirstOrDefault(p => p.Owner == owner) is not { } program) { return null; }
            if (program.Behaviors.All(b => _reported.TryGetValue(b.Id(owner), out var last) && last.Status != "Starting")) { return View(program); }
            var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _settling.Add((owner, reported));
            try { await reported.Task.WaitAsync(limit.Token); }
            catch (OperationCanceledException) { break; }
            finally { _settling.RemoveAll(waiter => waiter.Reported == reported); }
        }
        return Snapshot.Programs.FirstOrDefault(p => p.Owner == owner) is { } current ? View(current) : null;
    }

    private void NotifySettled(string owner)
    {
        foreach (var waiter in _settling.Where(waiter => waiter.Owner == owner).ToArray()) { waiter.Reported.TrySetResult(); }
    }

    // A behavior that exited while batches were in flight failed those runs. They are no longer started, so
    // the batch resent to the restarted behavior starts a run again.
    private void Failed(string owner, string hash, BehaviorStatusReport status)
    {
        foreach (var subscription in Snapshot.Subscriptions.Where(s => s.Behavior == status.Behavior && !IsRunFacts(s.Neuron)))
        {
            var inFlight = _started.Where(key => key.Subscription == subscription.Id).ToArray();
            if (inFlight.Length == 0) { continue; }
            var watermark = inFlight.Max(key => key.Watermark);
            _started.ExceptWith(inFlight);
            Ran(new BehaviorRan(RuntimeId, owner, status.Behavior, hash, subscription.Neuron, subscription.Signal, watermark,
                RunOutcome.Failed, inFlight.Length, 0, 0, status.Error), _batchTraces.GetValueOrDefault((subscription.Id, watermark)));
        }
    }

    // The pull path: the edge streams every batch returned here, so each starts its run.
    public Task<IReadOnlyList<SignalBatchEvent>> PendingBatches(string session)
    {
        RequireSpeaking(session);
        _facts.Clear();
        var pending = Snapshot.Subscriptions.Where(s => s.Pending.Length > 0).ToArray();
        IReadOnlyList<SignalBatchEvent> batches = [.. pending.Select(Batch)];
        foreach (var subscription in pending) { Started(subscription); }
        Flush();
        return Task.FromResult(batches);
    }

    public async Task<IReadOnlyList<AssignmentEvent>> Assignments(string session)
    {
        RequireSpeaking(session);
        var assigned = new List<AssignmentEvent>();
        foreach (var program in Snapshot.Programs)
        { foreach (var behavior in program.Behaviors) { assigned.Add(await AssignmentFor(program, behavior)); } }
        return assigned;
    }

    // A reactivated runtime has no source's acceptance yet: each source is watched on its lane, off every turn,
    // and only then is its log read, in a short turn of its own.
    public Task CatchUp()
    {
        foreach (var neuron in Snapshot.Subscriptions.Select(s => s.Neuron).Distinct(StringComparer.Ordinal).ToArray()) { _ = ReadAfterWatchAsync(neuron); }
        return Task.CompletedTask;
    }

    private async Task ReadAfterWatchAsync(string neuron)
    {
        await WatchOffTurn(neuron, again: false);
        try { await Self<IBrainRuntimeArrivals>(arrivals => arrivals.Read(neuron)); }
        catch (Exception error) { logger.LogDebug(error, "Brain {Brain} could not ask itself to read {Neuron}'s part log.", Brain, neuron); }
    }

    // One source spoke (its program's session saw it) or a reactivated runtime catches up: its log is read in this one-way turn.
    // Inspect interleaves, so a read in flight must not look idle.
    public async Task Read(string source)
    {
        _reading++;
        try { await CatchUpSourcesAsync([source]); }
        finally { _reading--; Flush(); }
    }

    // A source whose log cannot be read now is read again by the next reminder.
    private async Task CatchUpSourcesAsync(string[] sources)
    {
        _facts.Clear();
        foreach (var neuron in sources)
        {
            try { await CatchUpAsync(neuron); }
            catch (Exception error)
            {
                logger.LogWarning(error, "Brain {Brain} could not read {Neuron}'s part log from its watermark; its next reminder retries.", Brain, neuron);
            }
        }
        Flush();
    }

    // The source's log after each watermark is what is buffered, read through the source itself so it works on any
    // silo: a signal the runtime never heard live arrives too and none arrives twice. Only a subscription its source
    // accepted on this activation is buffered; any other keeps its watermark.
    private async Task CatchUpAsync(string neuron, bool push = true)
    {
        var matching = Snapshot.Subscriptions.Where(s => s.Neuron == neuron && Accepted(s)).ToArray();
        if (matching.Length == 0) { return; }
        var since = matching.Min(s => s.SourceSeq);
        if (!PlainSources.TryParse(neuron, out var plain)) { return; }
        var (floor, signals) = await ReadPlainAfterAsync(plain, since);
        var cap = options.Value.MaximumPending;
        var arrived = new List<Subscription>();
        var drops = new List<(Subscription Buffered, long Count)>();
        var changed = false;
        var next = Snapshot.Subscriptions.Select(s =>
        {
            if (s.Neuron != neuron || !Accepted(s)) { return s; }
            if (floor > s.SourceSeq)
            { logger.LogWarning("SubscriptionGap {Subscription} {Neuron}: its watermark {Watermark} is below seq {Floor}, the oldest its source's log still serves.", s.Id, neuron, s.SourceSeq, floor); }
            var buffered = s;
            foreach (var item in signals.Where(j => j.Sequence > s.SourceSeq && j.Type == s.Signal))
            {
                if (!SubscriptionFilter.Delivers(s.Behavior, item.Type, item.Json) || !SubscriptionFilter.AcceptsPlain(s.Selector, item.Type, item.Json)) { continue; }
                buffered = buffered.Arrive(item.Json, cap);
            }
            var through = Math.Max(Math.Max(s.SourceSeq, floor), signals.Count == 0 ? 0 : signals[^1].Sequence);
            buffered = buffered with { SourceSeq = through };
            if (buffered.Pending.Length != s.Pending.Length || buffered.Delivered != s.Delivered) { arrived.Add(buffered); }
            if (buffered.Dropped > s.Dropped) { drops.Add((buffered, buffered.Dropped - s.Dropped)); }
            changed |= buffered != s;
            return buffered;
        }).ToArray();
        if (!changed) { return; }
        await Save(Snapshot with { Subscriptions = next }, Changed());
        foreach (var (buffered, count) in drops) { Dropped(buffered, count); }
        if (push && Snapshot.Session is { Status: SessionStatus.Connected } session)
        {
            foreach (var subscription in arrived) { Push(session.Id, subscription); }
        }
    }

    // A signal the runtime buffers: a plain neuron's [Signal] record, typed by its alias, already JSON.
    private sealed record Arrival(long Sequence, string Type, string Json);

    // The plain publisher's signals in its brain's log after since, paged like ReadAfterAsync.
    private async Task<(long Floor, List<Arrival> Signals)> ReadPlainAfterAsync(DigitalBrain.NeuronId source, long since)
    {
        var signals = new List<Arrival>();
        var floor = 0L;
        var limit = Math.Min(options.Value.MaximumPending, DigitalBrain.SignalPage.MaxLimit);
        while (true)
        {
            var page = await Journal.ReadSince(source.Brain.Value, source.ToString(), since, limit, CancellationToken.None);
            floor = Math.Max(floor, page.Floor);
            since = Math.Max(since, page.Floor);
            foreach (var entry in page.Signals) { signals.Add(new(entry.Sequence, entry.Type, Encoding.UTF8.GetString(entry.Json.Span))); }
            if (page.Signals.Count == 0) { return (floor, signals); }
            since = page.Signals[^1].Sequence;
            if (page.Last <= since) { return (floor, signals); }
        }
    }

    // The reconcile reminder, and the subscription renewal timer (both reach here as ticks of the runtime host, 06-08).
    protected override async Task OnReminder(string reminderName, CancellationToken cancellationToken)
    {
        if (reminderName == RenewalTimer) { await RenewWatches(); return; }
        if (reminderName != ReconcileReminder) { return; }
        if (Snapshot.Programs.Length == 0) { await EndSessionAsync(); return; }
        if (_plain.Count == 0 && Snapshot.Subscriptions.Length > 0) { await WatchSourcesAsync(); }
        await CatchUpSourcesAsync([.. Snapshot.Subscriptions.Select(s => s.Neuron).Distinct(StringComparer.Ordinal)]);
        var before = Snapshot.Session?.Id;
        await EnsureSessionAsync();
        if (Snapshot.Session is not { } session || session.Id != before) { return; }
        // Rotated only once the host has it; a rotation its stream missed is retried on the next reminder.
        if (session.TokenIssued < time.GetUtcNow() - TokenRotation
            && streams.TryPush(session.Id, TokenEvent.Kind, new TokenEvent(tokens.Issue(Brain, session.Id))))
        { await Save(Snapshot with { Session = session with { TokenIssued = time.GetUtcNow() } }, Changed()); }
    }

    // A source's own signal may be what activated this runtime, so waiting for its watch now would deadlock;
    // the catch-up watches every source on its lane, off the activation turn, before it reads the source's log.
    internal Task ResumeAsync()
    {
        directory.Publish(RuntimeId, Snapshot);
        // Off this activation turn: publishing a run the previous process saved can wake a subscriber here.
        _ = PublishOwedRuns();
        if (Snapshot.Subscriptions.Length > 0)
        {
            SyncWatched();
            // Off the activation turn: a source's log may be on a part this machine has yet to open.
            _ = Self<IBrainRuntimeArrivals>(arrivals => arrivals.CatchUp());
        }
        // Signals may have arrived while watches were absent. Relaunch disposable behaviors so
        // their subscribe-then-read recovery runs against durable state, with fresh credentials.
        return Snapshot.Programs.Length > 0 ? EnsureSessionAsync(restart: true) : Task.CompletedTask;
    }

    private async Task PublishOwedRuns()
    {
        try { await Runs.Announce(); }
        catch (Exception error) { logger.LogDebug(error, "Brain {Brain} could not publish a run revision saved before it woke.", Brain); }
    }

    // A new session id per launch: the previous session's token stops speaking for this brain.
    // One rule for assignment, sign-in and the reminder. A missing, refused or silent session is replaced by
    // a new one (the old token stops speaking); a live one is ensured again, refreshing its token or
    // restoring it if the host lost it. Heartbeats are the detector; nothing here probes the host.
    // A brain created while its silo is still starting has no address a sandbox can dial yet: the program
    // is already assigned; ApplicationStarted retries it as soon as the HTTP edge listens.
    // A silo that listens, but on no http:// address, with no EdgeUrl configured, never will: that is a
    // misconfiguration, reported on every reminder, and the assignment still stands.
    private async Task EnsureSessionAsync(bool restart = false)
    {
        if (await PrepareSessionAsync(restart) is { } start) { OffTurn(runner.Ensure, start, "start"); }
        else if (ServerAddresses() is { Count: > 0 } addresses)
        { logger.LogError("Brain {Brain}'s sandbox session cannot start on {Addresses}: {Reason}", Brain, string.Join(", ", addresses), NotListening().Message); }
        else
        {
            deferredStarts.Defer(Brain);
            logger.LogInformation("Brain {Brain}'s sandbox session waits until this silo listens; HTTP startup retries it and its reminder is the fallback.", Brain);
        }
    }

    private async Task<SessionStart?> PrepareSessionAsync(bool restart)
    {
        var session = Snapshot.Session;
        if (restart || session is null || session.Status == SessionStatus.Refused || Silent(session))
        {
            session = new(Guid.NewGuid().ToString("N"), SessionStatus.Starting, null, null, time.GetUtcNow(), time.GetUtcNow());
            await Save(Snapshot with { Session = session, Initialized = new Dictionary<string, string>() }, Changed());
            await Context.Reminders.Start(ReconcileReminder, ReconcilePeriod, ReconcilePeriod);
        }
        return EdgeUrl() is { } edge ? new SessionStart(Brain, session.Id, tokens.Issue(Brain, session.Id), edge) : null;
    }

    private bool Silent(SessionRecord session) => (session.LastHeartbeat ?? session.Started) < time.GetUtcNow() - SilenceLimit;

    // The sandbox is called off this runtime's turn: a host that is down, slow or behind a proxy that never
    // answers must not hold the brain's runtime. The calls still reach the host in the order they were made,
    // so a stop never lands after the start that follows it. A failure is the reminder's to retry.
    private void OffTurn<T>(Func<T, CancellationToken, Task> call, T argument, string what)
    {
        var brain = Brain;
        var previous = _sandboxCalls;
        _sandboxCalls = Task.Run(async () =>
        {
            await previous;
            try { await call(argument, CancellationToken.None); }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
            { logger.LogWarning(error, "The sandbox did not {What} brain {Brain}'s session; its reminder retries.", what, brain); }
            catch (Exception error)
            { logger.LogError(error, "Could not {What} brain {Brain}'s sandbox session; its reminder retries.", what, brain); }
        });
    }

    private async Task EndSessionAsync()
    {
        if ((await Context.Reminders.Names()).Contains(ReconcileReminder)) { await Context.Reminders.Stop(ReconcileReminder); }
        if (Snapshot.Session is null) { return; }
        OffTurn(runner.Stop, Brain, "stop");
        await Save(Snapshot with { Session = null }, Changed());
    }

    private bool Speaks(string session)
        => Snapshot.Session is { Status: not SessionStatus.Refused } current && session.Length > 0 && session == current.Id;

    private void RequireSpeaking(string session)
    {
        if (!Speaks(session)) { throw new UnauthorizedAccessException("This session no longer speaks for its brain."); }
    }

    private async Task<AssignmentEvent> AssignmentFor(ProgramAssignment program, BehaviorAssembly behavior)
        => new(behavior.Id(program.Owner), behavior.Hash,
            Reachable(await assemblies.ReadUrl(behavior.Hash, AssemblyUrlLifetime, CancellationToken.None)),
            Reachable(await assemblies.ReadUrl(behavior.Hash + ".pdb", AssemblyUrlLifetime, CancellationToken.None)),
            new(program.Settings, StringComparer.Ordinal), AssignmentRevision(program, behavior));

    private string AssignmentRevision(ProgramAssignment program, BehaviorAssembly behavior)
        // Pre-generation persisted assignments get a stable legacy identity until their first Apply.
        // Every Apply thereafter uses a durable generation, so A -> B -> A cannot revive A's old marker.
        => Snapshot.AssignmentRevisions.GetValueOrDefault(behavior.Id(program.Owner))
        ?? "legacy-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            behavior.Hash,
            Settings = program.Settings.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(),
            program.Principal,
        }))));

    // Local blob storage answers on this machine's loopback, which no sandbox can reach; the Docker host
    // can. A SAS signs the path and query, not the host, so the rewritten URL stays valid.
    internal static Uri Reachable(Uri url) => url.IsLoopback ? new UriBuilder(url) { Host = DockerHost }.Uri : url;

    // A plain signal carries no trace of its own (06-08), so a batch carries none either; a batch trace recorded for a run (none
    // today) would be remembered until its ack, so the ack prices the CPU the run cost under it.
    private static SignalBatchEvent Batch(Subscription subscription) => new(subscription.Id, subscription.Pending, subscription.Watermark);

    private void Push(string session, Subscription subscription)
    {
        if (streams.TryPush(session, SignalBatchEvent.Kind, Batch(subscription))) { Started(subscription); }
    }

    // A batch on a live stream starts a run once per watermark: the same batch pushed again starts nothing.
    private void Started(Subscription subscription)
    {
        if (IsRunFacts(subscription.Neuron) || Synapse(subscription.Behavior) is not { } synapse
            || !_started.Add((subscription.Id, subscription.Watermark))) { return; }
        _facts.Add(RunFact.Of(new BehaviorStarted(RuntimeId, synapse.Owner, subscription.Behavior, synapse.Hash, subscription.Neuron,
            subscription.Signal, subscription.Watermark), _batchTraces.GetValueOrDefault((subscription.Id, subscription.Watermark))?.Trace ?? TraceContext.Current));
    }

    // Signals pushed out of a full buffer never reach the synapse: a run that cannot happen, counted, never silent.
    private void Dropped(Subscription buffered, long count)
    {
        if (IsRunFacts(buffered.Neuron) || Synapse(buffered.Behavior) is not { } synapse) { return; }
        Ran(new BehaviorRan(RuntimeId, synapse.Owner, buffered.Behavior, synapse.Hash, buffered.Neuron, buffered.Signal, buffered.Delivered,
            RunOutcome.Dropped, (int)Math.Min(count, int.MaxValue), 0, 0, null), null);
    }

    private void Ran(BehaviorRan ran, BatchTrace? batch)
    {
        var trace = batch?.Trace ?? TraceContext.Current;
        _facts.Add(RunFact.Of(ran, trace));
        var counts = _runs.GetValueOrDefault(ran.Behavior);
        _runs[ran.Behavior] = ran.Outcome switch
        {
            RunOutcome.Succeeded => counts with { Runs = counts.Runs + 1, LastDurationMicroseconds = ran.DurationMicroseconds },
            RunOutcome.Failed => counts with { Runs = counts.Runs + 1, Failures = counts.Failures + 1 },
            _ => counts,
        };
        logger.LogInformation("BehaviorRan {Behavior} {Outcome} {DurationMicroseconds}", ran.Behavior, ran.Outcome, ran.DurationMicroseconds);
        Span(ran, batch);
    }

    // The run as a span under the batch's own span, in the batch's trace, ending now and as long as the synapse
    // took. A parent without a span id would make .NET take the current request's span as parent, from another trace.
    private void Span(BehaviorRan ran, BatchTrace? batch)
    {
        var end = time.GetUtcNow();
        var parent = batch is not null && ActivityContext.TryParse(batch.TraceParent, null, out var context) ? context : default;
        using var activity = Telemetry.StartActivity("runtime.batch", ActivityKind.Internal, parent,
            startTime: end - TimeSpan.FromMicroseconds(ran.DurationMicroseconds));
        if (activity is null) { return; }
        activity.SetTag("intochatos.behavior", ran.Behavior).SetTag("intochatos.owner", ran.Owner)
            .SetTag("intochatos.source", ran.Source).SetTag("intochatos.signal", ran.SignalType)
            .SetTag("intochatos.outcome", ran.Outcome.ToString()).SetTag("intochatos.batches", ran.Batches)
            .SetTag("intochatos.watermark", ran.Watermark).SetTag("intochatos.cpu_us", ran.CpuMicroseconds);
        if (ran.Outcome == RunOutcome.Failed) { activity.SetStatus(ActivityStatusCode.Error, ran.Error); }
        activity.SetEndTime(end.UtcDateTime);
    }

    // Hand the turn's facts to the recording lane. Unstamped: the runtime is their author, whoever's call
    // produced them; the caller is restored for whatever the turn still does. The turn never waits
    // on publication, which can nudge this runtime back; Inspect includes the recording lane instead.
    // Nothing accepted is still being delivered, nothing delivered is still running, no batch is
    // waiting on a behavior, and no neuron still owes this brain a signal. A buffered subscription is
    // the next script, even before the sandbox pulls it.
    private bool ChainIdle() => _reading == 0 && _started.Count == 0 && _continuations.Count == 0
        && !Snapshot.Subscriptions.Any(subscription => subscription.Pending.Length > 0);

    private void Flush()
    {
        var idle = ChainIdle();
        if (_facts.Count == 0)
        {
            if (_announceOwed && idle) { BeginRecording(); }
            return;
        }
        // A completion with nothing else started, and no accepted signal still on its way, is the end
        // of this chain. Intermediate completions stay in the snapshot and wake nobody until then.
        if (idle) { _announceRuns = true; _announceOwed = false; }
        else { _announceOwed = true; }
        _pendingRecords.AddRange(_facts);
        _facts.Clear();
        BeginRecording();
    }

    private void BeginRecording()
    {
        if (!_recording.IsCompleted) { return; }
        var caller = RequestContext.Get(CallerContextStamper.RequestContextKey);
        RequestContext.Remove(CallerContextStamper.RequestContextKey);
        try { _recording = RecordAsync(); }
        finally { if (caller is not null) { RequestContext.Set(CallerContextStamper.RequestContextKey, caller); } }
    }

    private async Task RecordAsync()
    {
        // Send immediately when idle. Facts queued while a commit is in flight share the next
        // ordered append, retaining every fact's own trace without a timer or delaying a turn.
        // Announce yields, so a completion that lands during it has to be written before this lane ends.
        var runs = Runs;
        while (true)
        {
            if (_pendingRecords.Count > 0)
            {
                RunFact[] facts = [.. _pendingRecords];
                _pendingRecords.Clear();
                var announce = _announceRuns;
                _announceRuns = false;
                if (announce) { _announceOwed = false; }
                try { await runs.Record(facts, announce); }
                catch (Exception error)
                {
                    // The batch stays queued for the next completion or for shutdown. Retrying here would spin.
                    _recordingFailed = true;
                    _announceOwed = true;
                    _pendingRecords.InsertRange(0, facts);
                    if (announce) { _announceRuns = true; }
                    logger.LogError(error, "Brain {Brain} could not record its synapse runs.", Brain);
                    return;
                }
                continue;
            }
            if (!_announceOwed || !ChainIdle()) { return; }
            _announceOwed = false;
            try { await runs.Announce(); }
            catch (Exception error)
            {
                _recordingFailed = true;
                _announceOwed = true;
                logger.LogError(error, "Brain {Brain} could not publish its run revision.", Brain);
                return;
            }
        }
    }

    private readonly record struct RunCounts(long Runs, long Failures, long LastDurationMicroseconds);

    // A source that retains the signal seeds a new subscription with the backlog the synapse missed. Any other
    // source, a runs neuron among them, contributes nothing.
    private async Task<string[]> SeedRetainedAsync(string neuron, string signal, string? selector)
    {
        if (!PlainSources.TryParse(neuron, out var plain)) { return []; }
        // A plain app keeps no retention of its own (06-08): its journal is the backlog. The app's requests seed the last
        // RetainedRequests of them, its activity the last one, as the grain's retention did; any other plain signal seeds nothing.
        var keep = signal == SubscriptionFilter.RequestedAlias ? RetainedRequests : signal == SubscriptionFilter.ActivityAlias ? 1 : 0;
        if (keep == 0) { return []; }
        var (_, entries) = await ReadPlainAfterAsync(plain, 0);
        return [.. entries.Where(entry => entry.Type == signal && SubscriptionFilter.AcceptsPlain(selector, entry.Type, entry.Json))
            .TakeLast(keep).Select(entry => entry.Json)];
    }

    protected override void OnCommitted() => directory.Publish(RuntimeId, Snapshot);

    private ProgramView View(ProgramAssignment program)
        => new(program.Owner, program.Hash, [.. program.Behaviors.Select(behavior =>
        {
            var id = behavior.Id(program.Owner);
            (string Status, string? Error) reported = _reported.TryGetValue(id, out var last) ? last : ("Starting", null);
            var runs = _runs.GetValueOrDefault(id);
            return new BehaviorView(id, behavior.Hash, reported.Status, reported.Error,
                [.. Snapshot.Subscriptions.Where(s => s.Behavior == id).Select(s => new CSharpSubscriptionView(s.Neuron, s.Signal, s.Pending.Length, s.Delivered, s.Dropped))],
                runs.Runs, runs.Failures, runs.LastDurationMicroseconds);
        })]);

    private BrainRuntimeChanged Changed() => new(RuntimeId);

    // The host dials the brain's own edge: configured, or this silo's HTTP address on the Docker host.
    private string? EdgeUrl()
    {
        var root = deployment.Value.EdgeUrl is { Length: > 0 } configured ? configured : ListeningUrl();
        return root is null ? null : root.TrimEnd('/') + "/brains/" + Uri.EscapeDataString(Brain) + "/";
    }

    private static InvalidOperationException NotListening()
        => new($"Configure {CSharpOptions.SectionName}:EdgeUrl; this brain listens on no HTTP address a sandbox can reach.");

    // Empty while the silo has yet to bind its listeners.
    private ICollection<string>? ServerAddresses() => ServiceProvider.GetService<IServer>()?.Features.Get<IServerAddressesFeature>()?.Addresses;

    private string? ListeningUrl()
    {
        var listening = ServerAddresses()?.FirstOrDefault(address => address.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
        if (listening is null) { return null; }
        var wildcard = listening.Replace("://+", "://localhost", StringComparison.Ordinal).Replace("://*", "://localhost", StringComparison.Ordinal)
            .Replace("[::]", "localhost", StringComparison.Ordinal).Replace("0.0.0.0", "localhost", StringComparison.Ordinal);
        return new UriBuilder(wildcard) { Host = DockerHost }.Uri.ToString();
    }

    // Every source is watched off the turn, on its own lane (WatchOffTurn); a turn waits only for the source of
    // the subscription it is making, whose verdict it answers.
    private async Task WatchSourcesAsync(Subscription? required = null)
    {
        foreach (var gone in SyncWatched()) { _ = WatchOffTurn(gone); }
        if (required is not null)
        {
            // Adding a subscription changes only this source's selection. Rewatching all
            // previous sources here makes registration quadratic and repeats authorization.
            // Full reconciliation still runs on program changes and periodic lease renewal.
            await PlainWatchAsync(required.Neuron, required);
        }
        else
        {
            foreach (var key in _plain.ToArray()) { _ = WatchOffTurn(key); }
        }
    }

    // A source's lane: one watch of a source at a time, so a source that hangs holds only its own lane until its call
    // times out, and at most ConcurrentWatches sources are asked at once. Nothing awaits a lane inside a turn. A lane
    // asked again while it runs runs once more, against the state of then; a renewal only joins it.
    private Task WatchOffTurn(string key, bool again = true)
    {
        if (_lanes.TryGetValue(key, out var lane))
        {
            if (again) { _rewatch.Add(key); }
            return lane;
        }
        lane = LaneAsync(key);
        if (!lane.IsCompleted) { _lanes[key] = lane; }
        return lane;
    }

    private async Task LaneAsync(string key)
    {
        try
        {
            do
            {
                _rewatch.Remove(key);
                await _watchSlots.WaitAsync();
                try { await ReconcileAsync(key); }
                finally { _watchSlots.Release(); }
            }
            while (_rewatch.Contains(key));
        }
        finally { _lanes.Remove(key); }
    }

    // A source some subscription names is subscribed as each of its programs; one no subscription names any more is left.
    private async Task ReconcileAsync(string key)
    {
        try { await PlainReconcileAsync(key); }
        catch (Exception error)
        { logger.LogDebug(error, "Brain runtime {Runtime} could not watch or unwatch {Source}.", RuntimeId, key); }
    }

    private const int ConcurrentWatches = 8;
    private readonly SemaphoreSlim _watchSlots = new(ConcurrentWatches);
    private readonly Dictionary<string, Task> _lanes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _rewatch = new(StringComparer.Ordinal);

    // A source that refuses the program refuses the subscription: none is kept for a later turn to feed, and a
    // source no other subscription names is let go.
    private async Task RefuseAsync(Subscription refused)
    {
        await Save(Snapshot with { Subscriptions = [.. Snapshot.Subscriptions.Where(s => s.Id != refused.Id)] }, Changed());
        _started.RemoveWhere(key => key.Subscription == refused.Id);
        foreach (var key in _batchTraces.Keys.Where(key => key.Subscription == refused.Id).ToArray()) { _batchTraces.Remove(key); }
        await WatchSourcesAsync();
    }

    private static bool IsRefusal(Exception error) => error is UnauthorizedAccessException or UntrustedCallerException;

    // A call on a source carries the program's caller and nothing of the turn's own: no caller when the program
    // has none. RequestContext is async-local, so the turn's caller is back once this returns.
    private static async Task AsProgram(CapabilityCaller? caller, Func<Task> call)
    {
        if (caller is not null) { CallerContextStamper.Stamp(caller); }
        else { RequestContext.Remove(CallerContextStamper.RequestContextKey); }
        await call();
    }

    // A verdict holds for the caller it was given to: a program that now speaks for another principal is fed
    // nothing until its source accepts it again.
    private bool Accepted(Subscription subscription)
        => Find(subscription.Behavior) is { } program && _accepted.TryGetValue((subscription.Neuron, program.Owner), out var caller)
            && caller == new BehaviorIdentity(program.Principal, program.Owner).Caller;

    // Per source and program, the caller the source last accepted a watch from on this activation (null: the
    // program has none). Memory only: a reactivated runtime asks every source again before it buffers anything for it.
    private readonly Dictionary<(string Source, string Program), CapabilityCaller?> _accepted = [];
    private readonly HashSet<(string Source, string Program)> _unwatchable = [];

    // One subscription per distinct source across every behavior; returns the sources no subscription names any more.
    // The renewal timer only starts lanes, so a renewal never holds the runtime's turn for long; it also keeps this activation alive.
    private string[] SyncWatched()
    {
        var sources = Snapshot.Subscriptions.Select(s => s.Neuron).Distinct(StringComparer.Ordinal).ToArray();
        var dropped = _plain.Except(sources, StringComparer.Ordinal).ToArray();
        foreach (var gone in dropped) { _plain.Remove(gone); }
        foreach (var source in sources.Where(PlainSources.Is)) { _plain.Add(source); }
        StopIdlePlainSessions();
        if (_plain.Count == 0)
        {
            if (_renewing) { Context.Timers.Stop(RenewalTimer); }
            _renewing = false;
        }
        else if (!_renewing)
        {
            Context.Timers.Start(RenewalTimer, SubscriptionRenewal, SubscriptionRenewal, keepAlive: true);
            _renewing = true;
        }
        return dropped;
    }

    private Task RenewWatches()
    {
        foreach (var key in _plain.ToArray()) { _ = WatchOffTurn(key, again: false); }
        return Task.CompletedTask;
    }

    // A plain source is subscribed as each program subscribed to it, on that program's session "{brain}/runtime.session/csharp/{owner}",
    // under the program's caller as a watch is (06-05b): the kernel mirrors that stamp onto the call, so the source's AdmitSynapse
    // reads it as SynapseRequest.Caller (R4-20). What it answers is the program's verdict on this activation, exactly as a watch's is. Subscribe is idempotent, so the renewal also restores a session
    // that expired while this runtime was away.
    private async Task PlainWatchAsync(string key, Subscription? required)
    {
        if (!PlainSources.TryParse(key, out var source)) { return; }
        var subscribed = Snapshot.Subscriptions.Where(s => s.Neuron == key).ToArray();
        var aliases = subscribed.Select(s => s.Signal).Distinct(StringComparer.Ordinal).ToArray();
        var programs = subscribed.Select(s => Find(s.Behavior)).OfType<ProgramAssignment>().DistinctBy(p => p.Owner).ToArray();
        foreach (var gone in _accepted.Keys.Where(verdict => verdict.Source == key && programs.All(p => p.Owner != verdict.Program)).ToArray())
        { _accepted.Remove(gone); }
        var requiredBy = required is not null && required.Neuron == key ? Find(required.Behavior)?.Owner : null;
        ExceptionDispatchInfo? refusal = null;
        foreach (var program in programs)
        {
            var caller = new BehaviorIdentity(program.Principal, program.Owner).Caller;
            try
            {
                await AsProgram(caller, () => PlainSession(PlainSources.SessionOf(program.Owner)).Subscribe(source, aliases));
                _accepted[(key, program.Owner)] = caller;
                _unwatchable.Remove((key, program.Owner));
                EnsurePlainSession(program.Owner);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                _accepted.Remove((key, program.Owner));
                if (program.Owner == requiredBy) { refusal = ExceptionDispatchInfo.Capture(error); }
                else if (_unwatchable.Add((key, program.Owner))) { logger.LogWarning(error, "Brain runtime {Runtime} could not subscribe to {Source} for {Program}; retrying quietly.", RuntimeId, key, program.Owner); }
                else { logger.LogDebug(error, "Brain runtime {Runtime} still cannot subscribe to {Source} for {Program}.", RuntimeId, key, program.Owner); }
            }
        }
        refusal?.Throw();
    }

    // A plain source some subscription names is subscribed again; one no subscription names any more leaves every program's session.
    private async Task PlainReconcileAsync(string key)
    {
        if (_plain.Contains(key)) { await PlainWatchAsync(key, required: null); return; }
        var verdicts = _accepted.Where(verdict => verdict.Key.Source == key).ToArray();
        foreach (var verdict in verdicts) { _accepted.Remove(verdict.Key); }
        _unwatchable.RemoveWhere(refused => refused.Source == key);
        if (!PlainSources.TryParse(key, out var source)) { return; }
        foreach (var owner in verdicts.Select(verdict => verdict.Key.Program).Concat(Snapshot.Programs.Select(program => program.Owner)).Distinct(StringComparer.Ordinal))
        { await PlainSession(PlainSources.SessionOf(owner)).Unsubscribe(source); }
        // A reassigned program's new build can subscribe to this source while the unsubscribe above is still on its way: the
        // source then sees the subscribe first and the unsubscribe last, and the new build would hear nothing. A source that
        // is named again is watched again, and read, because what it published during the gap was never pushed.
        if (_plain.Contains(key))
        {
            await PlainWatchAsync(key, required: null);
            await Self<IBrainRuntimeArrivals>(arrivals => arrivals.Read(key));
        }
    }

    // One long-poll per program session while it holds a plain source: each batch names the sources that spoke, and each is read
    // from its BrainLog in a turn of its own (the subscription's SourceSeq is the cursor, so a batch acknowledged here loses nothing).
    private void EnsurePlainSession(string owner)
    {
        if (_plainLoops.ContainsKey(owner)) { return; }
        var stop = new CancellationTokenSource();
        _plainLoops[owner] = stop;
        _ = PlainSessionAsync(owner, stop.Token);
    }

    private async Task PlainSessionAsync(string owner, CancellationToken stop)
    {
        var session = PlainSession(PlainSources.SessionOf(owner));
        long acked = 0;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                if (await session.Next(acked, stop) is not { } batch) { continue; }
                foreach (var spoke in batch.Signals.Select(signal => PlainSources.Prefix + signal.Source).Distinct(StringComparer.Ordinal).ToArray())
                {
                    if (Snapshot.Subscriptions.Any(s => s.Neuron == spoke)) { await Self<IBrainRuntimeArrivals>(arrivals => arrivals.Read(spoke)); }
                }
                acked = batch.Number;
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                logger.LogDebug(error, "Brain runtime {Runtime} could not read program {Program}'s plain session; retrying.", RuntimeId, owner);
                try { await Task.Delay(TimeSpan.FromSeconds(1), stop); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    // A program that is no longer assigned ends its session, which removes its synapses. An assigned program keeps its session even
    // while it holds no subscription: a reassigned program drops its old subscriptions before the new build subscribes again, and
    // an end sent in between would reach the session after the new build's subscribe and silence it.
    private void StopIdlePlainSessions()
    {
        var owners = Snapshot.Programs.Select(program => program.Owner).ToHashSet(StringComparer.Ordinal);
        foreach (var owner in _plainLoops.Keys.Where(owner => !owners.Contains(owner)).ToArray())
        {
            _plainLoops[owner].Cancel();
            _plainLoops.Remove(owner);
            _ = EndPlainSessionAsync(owner);
        }
    }

    private async Task EndPlainSessionAsync(string owner)
    {
        try { await PlainSession(PlainSources.SessionOf(owner)).End(); }
        catch (Exception error) { logger.LogDebug(error, "Brain runtime {Runtime} could not end program {Program}'s plain session; it expires.", RuntimeId, owner); }
    }
}
