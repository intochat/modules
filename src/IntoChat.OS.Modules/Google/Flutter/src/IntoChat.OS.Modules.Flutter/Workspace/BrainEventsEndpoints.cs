using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Connections;
using IntoChat.OS.Framework;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime.Signals;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace IntoChat.OS.Modules.Flutter.Workspace;

// GET /brains/{brainId}/brain/events: the whole brain as `brain-snapshot` server-sent events. The first
// snapshot lists every neuron the kernel knows; another follows each time one is created or retired.
internal static class BrainEventsEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Map(IEndpointRouteBuilder endpoints)
        => BrainRoutes.Group(endpoints).MapGet("/brain/events", Events);

    private static async Task Events(HttpContext http, ILoggerFactory logs, CancellationToken ct)
    {
        var brain = BrainEdge.Open(http.RequestServices, BrainScope.CurrentId());
        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        await http.Response.Body.FlushAsync(ct);
        await foreach (var snapshot in Snapshots(brain, BrainScope.CurrentId(), logs.CreateLogger("IntoChat.OS.Modules.Flutter.BrainEvents"), ct))
        {
            await http.Response.WriteAsync("event: brain-snapshot\ndata: " + snapshot + "\n\n", ct);
            await http.Response.Body.FlushAsync(ct);
        }
    }

    internal static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(10);

    // The runtime as last read. A read that fails keeps the previous answer, so a transient error never
    // turns the graph's edges off; it is logged once when it starts and once when it ends. A runtime that
    // keeps failing past `staleAfter` is no longer believed: the answer becomes none (nodes only, Idle).
    private sealed class RuntimeView(IBrainRuntime runtime, ILogger log, TimeSpan staleAfter)
    {
        private BrainRuntimeSnapshot? _known;
        private long? _failingSince;

        public TimeSpan? StaleIn => _failingSince is { } since ? staleAfter - Stopwatch.GetElapsedTime(since) : null;

        public async Task<BrainRuntimeSnapshot?> Read(CancellationToken ct)
        {
            try
            {
                _known = await runtime.Inspect(ct);
                if (_failingSince is not null) { log.LogInformation("The brain runtime answers again."); _failingSince = null; }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                if (_failingSince is null)
                {
                    log.LogWarning(error, "The brain runtime could not be read; the graph keeps its last synapses for {Seconds}s.", staleAfter.TotalSeconds);
                    _failingSince = Stopwatch.GetTimestamp();
                }
                else if (StaleIn <= TimeSpan.Zero) { _known = null; }
            }
            return _known;
        }
    }

    private static readonly TimeSpan Burst = TimeSpan.FromMilliseconds(100);

    // The first snapshot is written at once. Watches on the kernel's instance announcements and on the
    // brain runtime follow; once every watch is registered the brain is read again, so nothing that
    // happened in between is missed, and a snapshot identical to the last one is not sent. A burst of
    // changes becomes one snapshot. A faulted kernel watch ends the stream with its error, so the
    // client sees a failure, never a clean end. The runtime is optional: a brain without one has no synapses.
    internal static async IAsyncEnumerable<string> Snapshots(BrainEdge brain, string brainId, ILogger log, [EnumeratorCancellation] CancellationToken ct, TimeSpan? staleAfter = null, TimeSpan? retryEvery = null)
    {
        // A change flag: one pending change says everything a hundred would.
        var changes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var runtime = brain.Get<IBrainRuntime>(BrainRuntimeKeys.For(brainId));
        var view = new RuntimeView(runtime, log, staleAfter ?? StaleAfter);
        var waiting = 3;
        var retrying = 0;
        void Subscribed() { if (Interlocked.Decrement(ref waiting) == 0) { changes.Writer.TryWrite(true); } }
        _ = Watch(brain.On<InstanceCreated>(brain.Self(brainId), () => { Subscribed(); return Task.CompletedTask; }, stop.Token), required: true);
        _ = Watch(brain.On<InstanceRetired>(brain.Self(brainId), () => { Subscribed(); return Task.CompletedTask; }, stop.Token), required: true);
        _ = Watch(brain.On<BrainRuntimeChanged>(runtime, () => { Subscribed(); return Task.CompletedTask; }, stop.Token), required: false);
        try
        {
            var (json, key) = await Snapshot(brain, view, brainId, ct);
            Recheck();
            var last = key;
            yield return json;
            await foreach (var _tick in changes.Reader.ReadAllAsync(ct))
            {
                await Task.Delay(Burst, ct);
                while (changes.Reader.TryRead(out var more)) { _ = more; }
                (json, key) = await Snapshot(brain, view, brainId, ct);
                Recheck();
                if (key == last) { continue; }
                last = key;
                yield return json;
            }
        }
        finally { await stop.CancelAsync(); }

        // While the runtime fails it is read again at a fixed interval, even if nothing else changes, until a read
        // succeeds: one pending retry at most, so a quiet recovery reaches the graph and retries never stack.
        void Recheck()
        {
            if (view.StaleIn is null || Interlocked.CompareExchange(ref retrying, 1, 0) != 0) { return; }
            _ = Task.Delay(retryEvery ?? RetryEvery, stop.Token).ContinueWith(
                _ => { Volatile.Write(ref retrying, 0); changes.Writer.TryWrite(true); }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        async Task Watch<T>(IAsyncEnumerable<T> signals, bool required)
        {
            try { await foreach (var _ in signals) { changes.Writer.TryWrite(true); } }
            catch (OperationCanceledException) { }
            catch (Exception error) when (!required)
            {
                _ = error;
                Subscribed();
            }
            catch (Exception error) { changes.Writer.TryComplete(error); }
        }
    }

    private static async Task<(string Json, string Key)> Snapshot(BrainEdge brain, RuntimeView view, string brainId, CancellationToken ct)
    {
        var running = await view.Read(ct);
        var behaviors = running?.Behaviors ?? [];
        var connected = running?.Session == SessionStatus.Connected;

        List<string> keys = [];
        List<(string Key, string Type)> instances = [];
        await foreach (var instance in brain.Kernel.Instances(null, ct)) { instances.Add((instance.Key, instance.Type)); }
        keys.AddRange(instances.Select(instance => instance.Key));

        string? AppOf(string behavior) => keys
            .Where(key => IApp.NameOf(brainId, key) is not null && (behavior == key || behavior.StartsWith(key + "/", StringComparison.Ordinal)))
            .OrderByDescending(key => key.Length).FirstOrDefault();

        List<object> nodes = [];
        foreach (var (key, type) in instances)
        {
            var name = key[(key.LastIndexOf('/') + 1)..];
            var active = connected && behaviors.Any(behavior => AppOf(behavior.Id) == key);
            nodes.Add(new { id = key, type, name, label = name, module = "", role = "observed", status = active ? "Running" : "Idle", handledSignals = Array.Empty<string>() });
        }

        List<object> synapses = [];
        foreach (var behavior in behaviors)
        {
            if (AppOf(behavior.Id) is not { } app) { continue; }
            foreach (var subscription in behavior.Subscriptions)
            {
                var source = GrainId.TryParse(subscription.Neuron, out var grain) ? grain.Key.ToString()! : subscription.Neuron;
                synapses.Add(new
                {
                    id = $"{behavior.Id}|{source}|{subscription.Signal}",
                    sourceId = source,
                    targetId = app,
                    signalType = subscription.Signal,
                    kind = "subscription",
                    fireCount = subscription.Delivered,
                });
            }
        }
        var identity = JsonSerializer.Serialize(new { nodes, synapses }, Json);
        var json = JsonSerializer.Serialize(new
        {
            rootId = brainId,
            observedAt = DateTimeOffset.UtcNow,
            scope = "brain",
            truncated = false,
            nodes,
            synapses,
            activity = Array.Empty<object>(),
            correlations = Array.Empty<object>(),
        }, Json);
        return (json, identity);
    }
}
