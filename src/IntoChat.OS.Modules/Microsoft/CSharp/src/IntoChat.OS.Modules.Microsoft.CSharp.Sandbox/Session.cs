using System.Collections.Concurrent;
using System.Text.Json;
using IntoChat.OS.Client.Runtime;
using IntoChat.OS.Client.Runtime.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

// One brain's connection and its loaded apps. The control loop follows the brain's stream: ready
// names the desired set, an assignment loads (or swaps) one app, a retirement unloads it, a logs request is answered from
// its ring buffer. Every load and every exit is reported. An app the brain did not retire is the
// host's to keep alive: an exit is reported and the app is loaded again after a growing backoff,
// until the brain retires it or assigns it a different hash.
public sealed class Session(SessionConnection connection, HttpClient http, IOptions<SandboxOptions> options, ILogger logger, TimeSpan? reloadBase = null, TimeProvider? time = null, TimeSpan? drainDeadline = null) : IAsyncDisposable
{
    private static readonly TimeSpan ReloadCap = TimeSpan.FromSeconds(60);

    private readonly TimeSpan _reloadBase = reloadBase ?? TimeSpan.FromSeconds(1);
    private readonly ConcurrentDictionary<string, AppRunner> _apps = new(StringComparer.Ordinal);
    // Per behavior, the assignment whose run last failed and how many times it has exited, which sizes
    // the next backoff. Held across reloads: only a clean exit, a new hash or a retirement forgets it.
    private readonly ConcurrentDictionary<string, FailedRun> _failures = new(StringComparer.Ordinal);
    // Per behavior, the assignment the host keeps alive, running or waiting out a backoff: set when an
    // assignment is loaded, dropped when it is retired. A reload carries the assignment it restarts and
    // fires only while that is still the one held here.
    private readonly ConcurrentDictionary<string, AssignmentEvent> _assigned = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _changes = new(1, 1);

    public IReadOnlyDictionary<string, AppRunner> Behaviors => _apps;
    public IReadOnlyDictionary<string, AppRunner> Apps => _apps;

    internal void RefreshToken(string token) => connection.TryUpdateToken(token);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await foreach (var (kind, data) in connection.Control.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                switch (kind)
                {
                    case ReadyEvent.Kind: await KeepOnlyAsync(JsonSerializer.Deserialize<ReadyEvent>(data, RuntimeProtocol.Json)!.Behaviors, cancellationToken).ConfigureAwait(false); break;
                    case AssignmentEvent.Kind: await AssignAsync(JsonSerializer.Deserialize<AssignmentEvent>(data, RuntimeProtocol.Json)!, cancellationToken).ConfigureAwait(false); break;
                    case RetireEvent.Kind: await RetireAsync(JsonSerializer.Deserialize<RetireEvent>(data, RuntimeProtocol.Json)!.Behavior, cancellationToken).ConfigureAwait(false); break;
                    case LogsRequestEvent.Kind: await AnswerLogsAsync(JsonSerializer.Deserialize<LogsRequestEvent>(data, RuntimeProtocol.Json)!, cancellationToken).ConfigureAwait(false); break;
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogError(error, "Session event {Kind} failed", kind);
            }
        }
    }

    private async Task AssignAsync(AssignmentEvent assignment, CancellationToken cancellationToken)
    {
        await _changes.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // A reconnecting stream resends every assignment: one already running as assigned keeps running.
            if (_apps.TryGetValue(assignment.Behavior, out var current) && Same(current.Assignment, assignment)) { return; }
            await LoadAsync(assignment, cancellationToken).ConfigureAwait(false);
        }
        finally { _changes.Release(); }
    }

    // Runs under the change gate. The assignment is held before it loads, so even one that fails to load
    // supersedes the reload an earlier one has pending.
    private async Task LoadAsync(AssignmentEvent assignment, CancellationToken cancellationToken)
    {
        var superseded = _assigned.TryGetValue(assignment.Behavior, out var earlier) ? earlier : null;
        _assigned[assignment.Behavior] = assignment;
        // A different hash is a different program, so the backoff an earlier one earned does not hold it back.
        if (_failures.TryGetValue(assignment.Behavior, out var failed) && failed.Assignment.Hash != assignment.Hash) { _failures.TryRemove(assignment.Behavior, out _); }
        // A swap unloads only this behavior; the others keep running.
        if (_apps.TryGetValue(assignment.Behavior, out var previous))
        {
            if (await DrainAsync(previous, assignment, superseded, cancellationToken).ConfigureAwait(false) is { } refusal)
            {
                await ReportAsync(new(assignment.Behavior, assignment.Hash, "UpdateFailed", null, refusal, assignment.Revision), cancellationToken).ConfigureAwait(false);
                return;
            }
        }
        AppRunner app;
        try { app = await AppRunner.LoadAsync(http, options, connection, assignment, logger, cancellationToken).ConfigureAwait(false); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning(error, "Behavior {Behavior} {Hash} did not load", assignment.Behavior, assignment.Hash);
            await ReportAsync(new(assignment.Behavior, assignment.Hash, "Exited", 1, error.Message, assignment.Revision), cancellationToken).ConfigureAwait(false);
            return;
        }
        _apps[assignment.Behavior] = app;
        logger.LogInformation("Loaded behavior {Behavior} {Hash}", assignment.Behavior, assignment.Hash);
        await ReportAsync(new(app.Id, app.Hash, "Running", null, null, app.Assignment.Revision), cancellationToken).ConfigureAwait(false);
        _ = ReportExitAsync(app);
    }

    // A reassign: fetch and check the new build, stop the old run taking new work and wait for its in-flight work, then unload it. Any
    // refusal (the build cannot be fetched or does not match its hash, the work does not finish in the deadline) leaves the old
    // run exactly as it was, still assigned and still receiving its signals, and says why.
    private async Task<string?> DrainAsync(AppRunner previous, AssignmentEvent assignment, AssignmentEvent? superseded, CancellationToken cancellationToken)
    {
        string? refusal = null;
        var deadline = drainDeadline ?? options.Value.DrainDeadline;
        try
        {
            await AppRunner.PrepareAsync(http, options, assignment, cancellationToken).ConfigureAwait(false);
            previous.Draining = true;
            if (await connection.PauseAsync(assignment.Behavior, deadline, time, cancellationToken).ConfigureAwait(false))
            {
                // The old run may have exited by itself meanwhile; then there is nothing left to stop.
                if (_apps.TryRemove(new(assignment.Behavior, previous))) { await previous.DisposeAsync().ConfigureAwait(false); }
                connection.Release(assignment.Behavior);
                logger.LogInformation("Drained and unloaded behavior {Behavior} {Hash}", previous.Id, previous.Hash);
                return null;
            }
            refusal = $"The running build still had work in flight after {deadline.TotalSeconds:0.##} seconds; it keeps running and {assignment.Hash} was not loaded.";
        }
        catch (Exception error) when (error is not OperationCanceledException)
        { refusal = $"The new build {assignment.Hash} could not be prepared: {error.Message}"; logger.LogWarning(error, "Preparing {Hash} for {Behavior} failed", assignment.Hash, assignment.Behavior); }
        previous.Draining = false;
        connection.Resume(assignment.Behavior);
        if (superseded is not null) { _assigned[assignment.Behavior] = superseded; }
        previous.Note(refusal);
        logger.LogWarning("Update of behavior {Behavior} to {Hash} failed: {Refusal}", assignment.Behavior, assignment.Hash, refusal);
        return refusal;
    }

    private static bool Same(AssignmentEvent running, AssignmentEvent assigned)
        => running.Hash == assigned.Hash && running.Revision == assigned.Revision && running.Settings.Count == assigned.Settings.Count
            && running.Settings.All(setting => assigned.Settings.TryGetValue(setting.Key, out var value) && value == setting.Value);

    private async Task RetireAsync(string behavior, CancellationToken cancellationToken)
    {
        await _changes.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await UnloadAsync(behavior, cancellationToken).ConfigureAwait(false); }
        finally { _changes.Release(); }
    }

    // The brain's whole desired set, sent on every (re)connect: a retirement pushed while the stream was down
    // never arrived, so whatever this session holds beyond the set is unloaded now.
    private async Task KeepOnlyAsync(string[] wanted, CancellationToken cancellationToken)
    {
        await _changes.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var behavior in _assigned.Keys.Except(wanted, StringComparer.Ordinal).ToArray())
            { await UnloadAsync(behavior, cancellationToken).ConfigureAwait(false); }
        }
        finally { _changes.Release(); }
    }

    private async Task UnloadAsync(string behavior, CancellationToken cancellationToken)
    {
        // Retired for good: a reload already scheduled for it is now unwanted.
        _assigned.TryRemove(behavior, out _);
        if (!_apps.TryRemove(behavior, out var app))
        {
            // An app that exited on its own holds nothing to stop, but the brain still expects the
            // retirement answered against the hash it last ran.
            if (_failures.TryRemove(behavior, out var failed))
            { await ReportAsync(new(behavior, failed.Assignment.Hash, "Stopped", 0, null, failed.Assignment.Revision), cancellationToken).ConfigureAwait(false); }
            return;
        }
        _failures.TryRemove(behavior, out _);
        await app.DisposeAsync().ConfigureAwait(false);
        logger.LogInformation("Unloaded behavior {Behavior} {Hash}", app.Id, app.Hash);
        await ReportAsync(new(app.Id, app.Hash, "Stopped", 0, null, app.Assignment.Revision), cancellationToken).ConfigureAwait(false);
    }

    // An exit the brain did not ask for: reported, then reloaded after a backoff for as long as the
    // assignment stands. A clean exit is not a failure, so it is not reloaded.
    private async Task ReportExitAsync(AppRunner app)
    {
        var exit = await app.Completion.ConfigureAwait(false);
        if (!_apps.TryRemove(new(app.Id, app))) { return; }
        // Counted before the exit is reported, so a retirement arriving with that report answers it as stopped.
        var attempt = 1;
        if (exit == 0) { _failures.TryRemove(app.Id, out _); }
        else
        {
            attempt = _failures.TryGetValue(app.Id, out var failed) && failed.Assignment.Hash == app.Hash ? failed.Count + 1 : 1;
            _failures[app.Id] = new(app.Assignment, attempt);
        }
        await ReportAsync(new(app.Id, app.Hash, "Exited", exit, app.Error, app.Assignment.Revision), connection.Stopping).ConfigureAwait(false);
        await app.DisposeAsync().ConfigureAwait(false);
        if (exit == 0) { return; }
        var delay = TimeSpan.FromTicks(Math.Min(_reloadBase.Ticks << Math.Min(attempt - 1, 6), ReloadCap.Ticks));
        _ = ReloadLaterAsync(app.Assignment, delay);
    }

    private async Task ReloadLaterAsync(AssignmentEvent assignment, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, time ?? TimeProvider.System, connection.Stopping).ConfigureAwait(false);
            await _changes.WaitAsync(connection.Stopping).ConfigureAwait(false);
            try
            {
                // Decided under the gate, so no assignment lands between the check and the load. A newer hash,
                // a resent assignment or a retirement during the backoff took this one's place, and a run
                // loaded meanwhile already keeps it alive.
                if (!_assigned.TryGetValue(assignment.Behavior, out var held) || !ReferenceEquals(held, assignment)
                    || _apps.ContainsKey(assignment.Behavior)) { return; }
                logger.LogInformation("Reloading behavior {Behavior} {Hash}", assignment.Behavior, assignment.Hash);
                await LoadAsync(assignment, connection.Stopping).ConfigureAwait(false);
            }
            finally { _changes.Release(); }
        }
        catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException) { logger.LogDebug(error, "Behavior reload ended: the session is closing"); }
    }

    private Task AnswerLogsAsync(LogsRequestEvent request, CancellationToken cancellationToken)
        => connection.ReportLogsAsync(request.Request, _apps.TryGetValue(request.Behavior, out var app) ? app.Tail(request.Tail) : "", cancellationToken);

    private async Task ReportAsync(BehaviorStatusReport status, CancellationToken cancellationToken)
    {
        try { await connection.ReportStatusAsync(status, cancellationToken).ConfigureAwait(false); }
        catch (Exception error) when (error is HttpRequestException or InvalidOperationException)
        { logger.LogWarning(error, "Could not report {Behavior} {Status}", status.Behavior, status.Status); }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var app in _apps.Values) { await app.DisposeAsync().ConfigureAwait(false); }
        _apps.Clear();
        await connection.DisposeAsync().ConfigureAwait(false);
        _changes.Dispose();
    }

    // A run that exited with a failure, and the hash it ran, kept until the failure count is forgotten.
    private sealed record FailedRun(AssignmentEvent Assignment, int Count);
}
