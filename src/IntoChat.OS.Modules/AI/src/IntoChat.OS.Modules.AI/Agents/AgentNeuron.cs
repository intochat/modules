using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using IntoChat.OS.Modules.AI.Agents.Signals;
using IntoChat.OS.Modules.AI.Inference;
using IntoChat.OS.Modules.AI.Models;
using IntoChat.OS.Framework;
using IntoChat.OS.Connections.Platform.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.AI.Agents;

// The agent's record as plain JSON state (06-06d; it keeps its state name "agent", clean break under SAFE-01). Every message
// held here is built from concrete arrays.
[ExportableState]
internal sealed record AgentStorage
{
    public long Revision { get; init; }
    public AgentDefinition Definition { get; init; } = new();
    public AiMessage[] Transcript { get; init; } = [];
    public string? Summary { get; init; }
    public AgentRun? ActiveRun { get; init; }
    public AgentRun[] Runs { get; init; } = [];
    public long Version { get; init; }
}

// A plain neuron since 06-06d. Every call interleaves (the contract says so), as the grain's did: a turn streams for minutes while
// Read, Cancel and Post reach the agent. The pending write (_beforeWrite, _write) keeps interleaving readers on committed versions;
// most writes publish nothing, so Persist saves through the runtime state directly and signals follow with Publish.
[DigitalBrain.Neuron("agent", State = "agent")]
internal sealed class AgentNeuron : AiNeuron<AgentStorage>, IAgent, IAgentDriver
{
    private const int KeptRuns = 64;
    // Replayed history is bounded by size as well as count: tool results (capability listings,
    // table queries) would otherwise grow every turn until the provider refuses the context.
    internal const int MaxHistoryCharacters = 120_000;
    internal const int MaxStoredToolResultCharacters = 4_000;
    // Every stored progress step writes the whole agent record and journals a signal, so streamed
    // text is stored and announced at most once per interval; the first words go at once, and a
    // turn's end, a tool step or a stalled provider take the rest.
    internal static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(250);
    private CancellationTokenSource? _active;
    private string? _cancelBeforeDrive;
    private bool _mutating;
    // Set only after Acquire has returned, and cleared only after Release has returned, so Drive's
    // tail does not pay a second grain call for a token the first signal already released.
    private bool _continuationHeld;
    // The agent's state as this activation holds it; the base's State is only what activation loaded.
    private AgentStorage _state = new();
    // Set when a write's outcome is unknown: the next call reloads the stored state and closes what it left open.
    private bool _stale;
    private new AgentStorage State => _state;
    // Commands still see the in-flight reservation (for admission and cancellation), but
    // interleaving readers must not expose a version before its write has committed.
    private AgentStorage? _beforeWrite;
    private TaskCompletionSource? _write;
    private AgentStorage Committed => _beforeWrite ?? State;
    private string Key => FullKey;
    // The brain the access check sees is the tools' and the meter's.
    private string Scope => BrainKey.ScopeOf(Key) ?? Key;

    private async Task AcquireContinuation(string runId)
    {
        if (ServiceProvider.GetService<IWorkContinuation>() is not { } gate) { return; }
        await gate.Acquire(Scope, "agent:" + runId);
        _continuationHeld = true;
    }

    private async Task ReleaseContinuation(string runId)
    {
        if (!_continuationHeld || ServiceProvider.GetService<IWorkContinuation>() is not { } gate) { return; }
        await gate.Release(Scope, "agent:" + runId);
        _continuationHeld = false;
    }

    // Everything announced or stored passes through the redactor: vault values are masked, never kept here.
    private ISecretRedactor Redactor => ServiceProvider.GetRequiredService<ISecretRedactor>();
    private async Task<string> Mask(string text) => (await Redactor.Redact(text, CancellationToken.None))!;

    private async Task<AiMessage> Mask(AiMessage message) => new(message.Role, [.. await MaskAll(message.Content)]);

    private async Task<AiContent[]> MaskAll(IReadOnlyList<AiContent> contents)
    {
        var masked = new List<AiContent>(contents.Count);
        foreach (var content in contents)
        {
            masked.Add(content switch
            {
                AiText text => text with { Text = await Mask(text.Text) },
                AiReasoning reasoning => reasoning with { Text = await Mask(reasoning.Text) },
                AiToolCall call => call with { ArgumentsJson = await Mask(call.ArgumentsJson) },
                AiToolResult result => result with { ResultJson = await Mask(result.ResultJson) },
                _ => content,
            });
        }
        return [.. masked];
    }

    private ILogger Logger => ServiceProvider.GetRequiredService<ILogger<AgentNeuron>>();
    private TimeProvider Clock => ServiceProvider.GetRequiredService<TimeProvider>();

    protected override async Task OnActivate(CancellationToken cancellationToken)
    {
        _state = base.State;
        await Resume();
    }

    // A run the previous activation left open is interrupted: external tool outcomes may be unknown.
    private async Task Resume()
    {
        if (State.ActiveRun is { } open)
        {
            const string Error = "Activation ended before the run committed; external tool outcomes may be unknown.";
            await Persist(State with { ActiveRun = null, Runs = Keep(State.Runs, Close(open, AgentRunStatus.Interrupted, Error)) });
            // Announced by a one-way call to the agent itself, so the request that woke the agent (a subscription, a
            // late Drive) is served first and a Start caller still gets the run's end.
            await Context.Get<IAgentDriver>(Id).Interrupted(open.RunId, Error);
        }
    }

    // After a write whose outcome is unknown the stored state is the truth: reload it and close the run it left open. The grain
    // deactivated for this; a plain activation reloads in place on its next call.
    private async Task Fresh()
    {
        if (!_stale || _active is not null || _mutating) { return; }
        _stale = false;
        _state = await Context.State.Load<AgentStorage>() ?? new();
        await Resume();
    }

    public async Task Interrupted(string runId, string error) => await Notify(new TurnFailed(Key, runId, await Mask(error), Cancelled: false));

    public async Task Configure(AgentDefinition definition, long expectedRevision)
    {
        await Fresh();
        EnsureIdle();
        ArgumentNullException.ThrowIfNull(definition);
        if (State.Revision != expectedRevision) { throw new InvalidOperationException("Agent revision conflict."); }
        if (definition.MaxModelCalls is < 1 or > 512 || definition.Timeout <= TimeSpan.Zero || definition.Timeout > TimeSpan.FromMinutes(60)
            || definition.MaxHistoryMessages is < 2 or > 4096) { throw new ArgumentException("Invalid agent execution limits.", nameof(definition)); }
        if (definition.Instructions.Length > 32000 || definition.Contracts.Length > 128 || definition.Contracts.Any(string.IsNullOrWhiteSpace)
            || definition.Contracts.Distinct(StringComparer.Ordinal).Count() != definition.Contracts.Length
            || definition.Contexts.Length > 32 || definition.Contexts.Any(key => string.IsNullOrWhiteSpace(key) || key.Length > 512))
        { throw new ArgumentException("Instructions, contracts or contexts are invalid.", nameof(definition)); }
        _mutating = true;
        try
        {
            await Persist(State with
            {
                Revision = State.Revision + 1,
                Definition = definition with
                {
                    Contracts = [.. definition.Contracts],
                    Capabilities = [.. definition.Capabilities],
                    RoutingExamples = [.. definition.RoutingExamples],
                    Contexts = [.. definition.Contexts],
                },
            });
            await Notify(new AgentConfigured(Key, State.Revision));
        }
        finally { _mutating = false; }
    }

    public async Task<AgentState> Read()
    {
        await Fresh();
        if (_write is { } pending) { await pending.Task; }
        var state = Committed;
        return new AgentState(state.Revision, state.Definition, state.Summary, state.ActiveRun, [.. state.Runs]) { Version = state.Version };
    }

    public async Task<AiMessage[]> Transcript()
    {
        await Fresh();
        if (_write is { } pending) { await pending.Task; }
        return [.. Committed.Transcript];
    }

    public Task Cancel()
    {
        if (_active is { } active) { active.Cancel(); }
        // A started run whose driver has not picked it up yet is cancelled when it does.
        else if (State.ActiveRun is { } pending) { _cancelBeforeDrive = pending.RunId; }
        return Task.CompletedTask;
    }

    public Task CancelRun(string runId)
    {
        // A delayed/replayed UI command must never cancel a later turn.
        return State.ActiveRun?.RunId == runId ? Cancel() : Task.CompletedTask;
    }

    public async Task Clear()
    {
        await Fresh();
        EnsureIdle();
        _mutating = true;
        try
        {
            await Persist(State with { Revision = State.Revision + 1, Transcript = [], Summary = null, Runs = [] });
            await Notify(new TranscriptCleared(Key, State.Revision));
        }
        finally { _mutating = false; }
    }

    public async Task Post(string text, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var runId = "post/" + id;
        await Fresh();
        // A pending post is not durable yet, so a duplicate must retry after its write commits.
        if (_mutating) { throw new InvalidOperationException("The agent is answering; post again when the run ends."); }
        // A replayed post (same id) never adds a second entry.
        if (State.Runs.Any(run => run.RunId == runId)) { return; }
        if (State.ActiveRun is not null) { throw new InvalidOperationException("The agent is answering; post again when the run ends."); }
        var now = DateTimeOffset.UtcNow;
        var run = new AgentRun(runId, AgentRunStatus.Completed, "", now, now, text) { Posted = true };
        _mutating = true;
        try
        {
            await Persist(State with { Transcript = [.. State.Transcript, new AiMessage("assistant", [new AiText(text)])], Runs = Keep(State.Runs, run) });
            await Notify(new TurnCompleted(Key, runId, text));
        }
        finally { _mutating = false; }
    }

    public async Task<string> Start(AgentTurn turn)
    {
        await Fresh();
        var (runId, replay) = Admit(turn);
        if (replay is not null)
        {
            await Notify(new TurnStarted(Key, runId, replay.UserText));
            await Notify(new TurnCompleted(Key, runId, await Mask(replay.Reply ?? "")));
            return runId;
        }
        await Persist(State with { ActiveRun = new AgentRun(runId, AgentRunStatus.Running, UserText(turn.Message), DateTimeOffset.UtcNow) });
        try
        {
            await AcquireContinuation(runId);
            var driven = turn with { RunId = runId };
            await Context.Get<IAgentDriver>(Id).Drive(driven, runId);
        }
        catch
        {
            // The caller learns why the hand-off failed; a failure to undo it must not replace that.
            try { await ReleaseContinuation(runId); }
            catch (Exception error) { Abandon(error, runId); }
            try { await Persist(State with { ActiveRun = null }); }
            catch (Exception error) { Abandon(error, runId); }
            throw;
        }
        return runId;
    }

    public async Task Drive(AgentTurn turn, string runId)
    {
        try { await foreach (var _ in Execute(turn, runId, CancellationToken.None)) { } }
        catch (Exception error) { Logger.LogWarning(error, "Agent turn {RunId} ended with an error.", runId); }
        finally { await ReleaseContinuation(runId); }
    }

    public IAsyncEnumerable<AgentTurnEvent> Run(AgentTurn turn, CancellationToken ct = default) => Execute(turn, null, ct);

    // started is the run id Start already admitted and recorded as active; null admits the turn here.
    private async IAsyncEnumerable<AgentTurnEvent> Execute(AgentTurn turn, string? started, [EnumeratorCancellation] CancellationToken ct)
    {
        string runId;
        AgentRun? replay;
        if (started is null) { await Fresh(); (runId, replay) = Admit(turn); }
        else if (State.ActiveRun?.RunId == started && _active is null) { (runId, replay) = (started, null); }
        else
        {
            // Whoever closed the run (a Resume, a Run that took it over) published its end; a run
            // recorded nowhere gets its end here, so a Start caller is never left waiting.
            Logger.LogWarning("Agent run {RunId} was no longer active when its driver arrived.", started);
            if (State.ActiveRun?.RunId != started && State.Runs.All(run => run.RunId != started))
            { await Notify(new TurnFailed(Key, started, "The run was lost before it started.", Cancelled: false)); }
            yield break;
        }
        if (replay is not null)
        {
            await Notify(new TurnStarted(Key, runId, replay.UserText));
            yield return new AgentTurnEvent.Started(runId);
            yield return new AgentTurnEvent.Text(replay.Reply ?? "");
            yield return new AgentTurnEvent.Completed([new AiMessage("assistant", [new AiText(replay.Reply ?? "")])], replay.Usage);
            await Notify(new TurnCompleted(Key, runId, await Mask(replay.Reply ?? "")));
            yield return new AgentTurnEvent.Finished();
            yield break;
        }

        var definition = State.Definition;
        var input = Stored(turn.Message);
        var run = State.ActiveRun is { } active && active.RunId == runId ? active : new AgentRun(runId, AgentRunStatus.Running, UserText(input), DateTimeOffset.UtcNow);
        // The timeout has its own source, so a run that ran out of time is told apart from a cancelled one.
        using var timeout = new CancellationTokenSource(definition.Timeout);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        var timedOut = $"The run exceeded its {definition.Timeout} timeout.";
        // Set before the first await, so an interleaved Run, Start, Configure or Clear sees this run.
        _active = lifetime;
        if (_cancelBeforeDrive == runId) { lifetime.Cancel(); }
        _cancelBeforeDrive = null;
        var completed = false;
        var yielding = false;
        var text = new StringBuilder();
        // How much of the text is announced, and when the run was last stored with its text.
        var shown = 0;
        // The text as masked and held back so far, and how much of the raw text it covers.
        var visible = "";
        var seen = 0;
        var stored = 0L;
        var sequence = 0;
        AgentTurnEvent.Failed? failure = null;

        // The run as streamed so far, including text not yet stored.
        AgentRun Streamed() => visible.Length > 0 ? run with { Reply = visible } : run;

        // Masks the streamed text; until it is complete, a tail that could still grow into a secret is held back.
        async Task Refresh(bool final)
        {
            var raw = text.ToString();
            var tail = final ? 0 : Math.Min(raw.Length, await Redactor.TailLength(CancellationToken.None));
            visible = await Mask(raw[..(raw.Length - tail)]);
            seen = raw.Length;
        }

        // Announces the text not yet announced; the caller stored it first, so the step carries that version.
        async Task Announce(bool persisted = true)
        {
            if (shown < visible.Length)
            {
                var part = visible[shown..];
                shown = visible.Length;
                await Notify(new TurnProgressed(Key, runId, "text", part, null, null, ++sequence), persisted);
            }
            stored = Clock.GetTimestamp();
        }

        async Task Store()
        {
            await Refresh(false);
            run = Streamed();
            await Persist(State with { ActiveRun = run });
            await Announce();
        }

        try
        {
            // Start already stored this run; only a Run admits it here.
            if (State.ActiveRun != run) { await Persist(State with { ActiveRun = run }); }
            await Notify(new TurnStarted(Key, runId, run.UserText));
            // The caller's script can finish as soon as Start returns. Release once the turn's first
            // signal is published, so later streamed progress still reaches the projection.
            await ReleaseContinuation(runId);
            var bounded = Bound(State.Transcript, Math.Max(1, definition.MaxHistoryMessages - 1), Math.Max(0, MaxHistoryCharacters - Size(input)), State.Summary);
            var plan = new TurnPlan(Scope, Key, runId, bounded.Messages, bounded.Summary, input,
                turn.Instructions ?? definition.Instructions, turn.Contracts ?? definition.Contracts, turn.Contexts ?? definition.Contexts,
                turn.Model ?? definition.Model, definition.Options, definition.MaxModelCalls, true);
            var loop = new AgentTurnLoop(ServiceProvider).Run(plan, lifetime.Token).GetAsyncEnumerator(lifetime.Token);
            try
            {
                while (true)
                {
                    var step = loop.MoveNextAsync();
                    if (!step.IsCompleted && seen < text.Length)
                    {
                        // Text held back by the interval is stored once the provider stalls, so what
                        // arrived is never hidden for longer than an interval.
                        var next = step.AsTask();
                        using var stalled = new CancellationTokenSource();
                        var due = ProgressInterval - Clock.GetElapsedTime(stored);
                        if (await Task.WhenAny(next, Task.Delay(due > TimeSpan.Zero ? due : TimeSpan.Zero, Clock, stalled.Token)) != next)
                        {
                            // The step is still running, so this write must not end the turn: the next
                            // store retries it, and that one fails the turn as any write does.
                            try { await Store(); }
                            catch (Exception error) { Logger.LogWarning(error, "Agent run {RunId} could not store its streamed text yet.", runId); }
                        }
                        await stalled.CancelAsync();
                        step = new(next);
                    }
                    if (!await step) { break; }
                    var current = loop.Current;
                    switch (current)
                    {
                        case AgentTurnEvent.ModelSelected selected:
                            run = run with { Model = selected.Model };
                            await Store();
                            break;
                        case AgentTurnEvent.Text delta:
                            text.Append(delta.Content);
                            // The first words are stored at once, later ones at most once per interval.
                            if (shown == 0 || Clock.GetElapsedTime(stored) >= ProgressInterval) { await Store(); }
                            break;
                        case AgentTurnEvent.ToolStarted tool:
                            var arguments = await Mask(tool.Arguments);
                            run = run with { ToolCalls = [.. run.ToolCalls, new AgentToolCall(tool.CallId, tool.Name, arguments)] };
                            await Store();
                            await Notify(new TurnProgressed(Key, runId, "tool-call", null, tool.Name, arguments, ++sequence));
                            break;
                        case AgentTurnEvent.ToolCompleted tool:
                            var result = await Mask(tool.Result);
                            var refusal = ToolRefusal(result);
                            run = run with
                            {
                                ToolCalls = Settle(run.ToolCalls, tool.CallId,
                                call => call with { Result = result, Code = refusal?.Code, Error = refusal?.Message })
                            };
                            await Store();
                            await Notify(new TurnProgressed(Key, runId, "tool-result", refusal?.Message, tool.Name, result, ++sequence));
                            if (Card(result) is { } card) { await Notify(new TurnProgressed(Key, runId, "card", null, tool.Name, card, ++sequence)); }
                            break;
                        case AgentTurnEvent.ToolFailed tool:
                            var message = await Mask(tool.Message);
                            run = run with { ToolCalls = Settle(run.ToolCalls, tool.CallId, call => call with { Code = tool.Code, Error = message }) };
                            await Store();
                            await Notify(new TurnProgressed(Key, runId, "tool-result", message, tool.Name,
                                JsonSerializer.Serialize(new { isError = true, code = tool.Code, message }), ++sequence));
                            break;
                        case AgentTurnEvent.Completed done:
                            await Refresh(true);
                            run = run with { Status = AgentRunStatus.Completed, EndedAt = DateTimeOffset.UtcNow, Reply = visible, Usage = done.Usage };
                            var produced = new List<AiMessage>(done.Messages.Count);
                            foreach (var item in done.Messages) { produced.Add(Compacted(await Mask(item))); }
                            var transcript = Bound([.. State.Transcript, input, .. produced], definition.MaxHistoryMessages, MaxHistoryCharacters, State.Summary);
                            await Persist(State with { Transcript = transcript.Messages, Summary = transcript.Summary, ActiveRun = null, Runs = Keep(State.Runs, run) });
                            completed = true;
                            await Announce();
                            if (done.Usage is not null) { await Notify(new TurnProgressed(Key, runId, "receipt", null, null, await Mask(JsonSerializer.Serialize(done.Usage)), ++sequence)); }
                            break;
                        case AgentTurnEvent.Failed failed:
                            // The loop sees only a cancelled token; a run out of time failed, it was not cancelled.
                            failure = failed.Cancelled && TimedOut(timeout, ct) ? new(timedOut, Cancelled: false) : failed;
                            current = failure;
                            break;
                    }
                    yielding = true;
                    yield return current;
                    yielding = false;
                }
            }
            finally { await loop.DisposeAsync(); }
        }
        finally
        {
            try
            {
                if (completed) { await Notify(new TurnCompleted(Key, runId, visible)); }
                else
                {
                    // Finally while yielding means the consumer stopped pulling: an abandoned stream is a cancelled run.
                    var expired = !yielding && TimedOut(timeout, ct);
                    // A recorded failure is authoritative; abandonment and the token only decide when none was recorded.
                    var cancelled = !expired && (failure is not null ? failure.Cancelled : yielding || lifetime.IsCancellationRequested);
                    var error = expired ? timedOut
                        : failure?.Message ?? (yielding ? "The response stream was abandoned." : "Execution failed before committing its result.");
                    // The error and the text so far are masked; a redactor that fails leaves the held-back text and a generic error.
                    try { await Refresh(true); error = await Mask(error); }
                    catch (Exception mask) { Logger.LogWarning(mask, "Agent run {RunId} could not mask its outcome.", runId); error = "Execution failed before committing its result."; }
                    run = Close(Streamed(), cancelled ? AgentRunStatus.Cancelled : AgentRunStatus.Failed, error, cancelled ? "tool_cancelled" : "tool_interrupted");
                    // The end is announced whether or not it could be stored: a Start caller must never wait forever.
                    var persisted = true;
                    try { await Persist(State with { ActiveRun = null, Runs = Keep(State.Runs, run) }); }
                    catch (Exception persist) { persisted = false; Abandon(persist, runId); }
                    await Announce(persisted);
                    await Notify(new TurnFailed(Key, runId, error, cancelled), persisted);
                }
            }
            finally { _active = null; }
        }
    }

    // Validates the turn and decides whether it is a fresh run or a replay of a retained one.
    private (string RunId, AgentRun? Replay) Admit(AgentTurn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        var message = turn.Message ?? throw new ArgumentException("An agent turn needs a message.", nameof(turn));
        if (message.Role != "user" || message.Content is not { Count: > 0 }) { throw new ArgumentException("An agent accepts a non-empty user message.", nameof(turn)); }
        if (message.Content.Any(content => content is not (AiText or AiImage or AiAudio)) || message.Content.OfType<AiText>().Sum(content => (long)content.Text.Length) is 0 or > 32000)
        { throw new ArgumentException("User messages carry 1-32000 characters of text, with images or audio, never tool calls or reasoning.", nameof(turn)); }
        if (turn.RunId is { Length: > 200 }) { throw new ArgumentException("A run id has at most 200 characters.", nameof(turn)); }
        if (turn.Contexts is { } contexts && (contexts.Length > 32 || contexts.Any(key => string.IsNullOrWhiteSpace(key) || key.Length > 512)))
        { throw new ArgumentException("A turn names at most 32 contexts, each 1-512 characters.", nameof(turn)); }
        _ = InferenceMapping.ToChatMessage(message);
        var runId = string.IsNullOrWhiteSpace(turn.RunId) ? Guid.NewGuid().ToString("N") : turn.RunId;
        if (Committed.Runs.FirstOrDefault(run => run.RunId == runId) is { } retained)
        {
            // A retained run id replays only for the same text; another text must never get an unrelated answer.
            if (retained.UserText != UserText(message)) { throw new InvalidOperationException("Run ID already belongs to another message."); }
            // A replay touches nothing, so it needs no idle agent; a run that did not complete is retried under its id.
            if (retained.Status == AgentRunStatus.Completed) { return (runId, retained); }
        }
        EnsureIdle();
        return (runId, null);
    }

    private void EnsureIdle()
    {
        if (_active is not null || _mutating || State.ActiveRun is not null) { throw new InvalidOperationException("This agent already has an active operation."); }
    }

    private static string UserText(AiMessage message) => string.Concat(message.Content.OfType<AiText>().Select(text => text.Text));

    // Snapshot input content before committing it to durable history.
    private static AiMessage Stored(AiMessage message) => new(message.Role, message.Content.ToArray());

    // A committed tool result keeps a bounded preview: the turn that called the tool saw all of it,
    // later turns only need to know what it was. The result stays valid JSON, paired with its call.
    private static AiMessage Compacted(AiMessage message) => new(message.Role, message.Content.Select(content => content switch
    {
        AiToolResult result when result.ResultJson.Length > MaxStoredToolResultCharacters => new AiToolResult(result.CallId, JsonSerializer.Serialize(new
        {
            truncated = true,
            characters = result.ResultJson.Length,
            preview = Preview(result.ResultJson),
        })),
        _ => content,
    }).ToArray());

    // Never cut a surrogate pair in half: the preview must stay valid UTF-16 to serialize.
    private static string Preview(string json)
        => json[..(char.IsHighSurrogate(json[MaxStoredToolResultCharacters - 1]) ? MaxStoredToolResultCharacters - 1 : MaxStoredToolResultCharacters)];

    // What a message costs the model in text; media is priced by the provider per item, not by its URI.
    private static long Size(AiMessage message) => message.Content.Sum(content => (long)(content switch
    {
        AiText text => text.Text.Length,
        AiReasoning reasoning => reasoning.Text.Length,
        AiToolCall call => call.Name.Length + call.ArgumentsJson.Length,
        AiToolResult result => result.ResultJson.Length,
        _ => 0,
    }));

    // Only the run's own time limit is a timeout; the caller's token and Cancel are cancellations.
    private static bool TimedOut(CancellationTokenSource timeout, CancellationToken caller) => timeout.IsCancellationRequested && !caller.IsCancellationRequested;

    // The stored state no longer matches what this activation holds; a fresh activation reads the
    // stored run back and Resume closes it as interrupted.
    private void Abandon(Exception error, string runId)
    {
        Logger.LogError(error, "Agent run {RunId} could not store its outcome; the agent recovers from storage on its next call.", runId);
        _stale = true;
    }

    private static AgentRun Close(AgentRun run, AgentRunStatus status, string error, string openCallCode = "tool_interrupted") => run with
    {
        Status = status,
        EndedAt = DateTimeOffset.UtcNow,
        Error = error,
        ToolCalls = [.. run.ToolCalls.Select(call => call.Result is null && call.Code is null
            ? call with { Code = openCallCode, Error = "Execution ended before the tool call completed." } : call)],
    };

    private static AgentRun[] Keep(AgentRun[] runs, AgentRun run) => [.. runs.Where(item => item.RunId != run.RunId).TakeLast(KeptRuns - 1), run];

    private static AgentToolCall[] Settle(AgentToolCall[] calls, string callId, Func<AgentToolCall, AgentToolCall> update)
        => [.. calls.Select(call => call.CallId == callId ? update(call) : call)];

    // Neuron<TState>.Save always publishes a signal; most agent writes have none, and a failed
    // signal must never undo a committed write, so the agent persists through the runtime state on its own.
    private async Task Persist(AgentStorage next)
    {
        var previous = _state;
        var writing = _write = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _beforeWrite = previous;
        _state = next with { Version = previous.Version + 1 };
        try
        {
            var writes = ServiceProvider.GetService<AgentWrites>();
            if (writes is not null) { await writes.Before(_state); }
            await Context.State.Save(_state, []);
            writes?.Committed(_state);
        }
        catch { _state = previous; throw; }
        finally
        {
            _beforeWrite = null;
            _write = null;
            writing.TrySetResult();
        }
    }

    private async Task Notify(DigitalBrain.Signal signal, bool persisted = true)
    {
        // Failed persistence needs a fresh Read to reactivate/recover the abandoned run,
        // even when a consumer already projected the last committed version.
        var version = persisted ? Committed.Version : 0;
        signal = signal switch
        {
            TurnStarted started => started with { Version = version },
            TurnProgressed progressed => progressed with { Version = version },
            TurnCompleted completed => completed with { Version = version },
            TurnFailed failed => failed with { Version = version },
            _ => signal,
        };
        try { await Publish(signal); }
        catch (Exception error) { Logger.LogWarning(error, "Agent signal delivery failed; consumers can reconcile through Read and Transcript."); }
    }

    // Recoverable tool refusals are results so the model can continue, but their activity
    // still failed. Keep the original result alongside the diagnostic used by projections.
    private static (string Code, string Message)? ToolRefusal(string result)
    {
        try
        {
            using var payload = JsonDocument.Parse(result);
            var root = payload.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("isError", out var error)
                || error.ValueKind != JsonValueKind.True) { return null; }
            var code = root.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            var message = root.TryGetProperty("message", out value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return (string.IsNullOrWhiteSpace(code) ? "tool_error" : code,
                string.IsNullOrWhiteSpace(message) ? "The tool reported an error." : message);
        }
        catch (JsonException) { return null; }
    }

    // A tool result may carry a UI card on its "_ui" channel; the card is its own progress item.
    private static string? Card(string result)
    {
        try
        {
            using var payload = JsonDocument.Parse(result);
            return payload.RootElement.ValueKind == JsonValueKind.Object && payload.RootElement.TryGetProperty("_ui", out var card)
                && card.ValueKind == JsonValueKind.Object ? card.GetRawText() : null;
        }
        catch (JsonException) { return null; }
    }

    // Overflow, by count or by size, folds the oldest messages into a bounded summary instead of failing the run.
    private static (AiMessage[] Messages, string? Summary) Bound(AiMessage[] messages, int maxMessages, long maxCharacters, string? summary)
    {
        var drop = Math.Max(0, messages.Length - maxMessages);
        var size = messages.Skip(drop).Sum(Size);
        while (drop < messages.Length && size > maxCharacters) { size -= Size(messages[drop]); drop++; }
        if (drop == 0) { return (messages, summary); }
        // Cut on a turn boundary: history always starts at a user message, so a tool call is never
        // kept without its result or a result without its call. A turn too long to keep goes whole.
        while (drop < messages.Length && messages[drop].Role != "user") { drop++; }
        var added = string.Join(" ", messages.Take(drop).Select(UserText)
            .Where(text => !string.IsNullOrWhiteSpace(text)).Select(text => text.Length <= 120 ? text : text[..120]));
        var combined = added.Length == 0 ? summary : string.IsNullOrEmpty(summary) ? added : summary + " " + added;
        return (messages[drop..], combined is { Length: > 4000 } ? combined[^4000..] : combined);
    }
}

// A test seam over the agent's own writes (06-06d): the runtime's journal is not a grain storage a fact can wrap, so the
// persistence and storage-fault facts register one to hold or fail a write. Never registered in production.
internal abstract class AgentWrites
{
    public abstract Task Before(AgentStorage next);

    public virtual void Committed(AgentStorage committed) { }
}
