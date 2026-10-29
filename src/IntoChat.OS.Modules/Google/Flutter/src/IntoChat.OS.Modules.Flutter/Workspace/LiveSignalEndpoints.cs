using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using DigitalBrain.Runtime.Orleans;
using DigitalBrain.Runtime.Orleans.Signalling;
using IntoChat.OS.Modules.Apps;
using Microsoft.Extensions.DependencyInjection;
using IntoChat.OS.Modules.Apps.Signals;
using IntoChat.OS.Modules.Circles;
using IntoChat.OS.Modules.Circles.Signals;
using IntoChat.OS.Connections;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Diagnostics;
using IntoChat.OS.Modules.Microsoft.Aspire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Orleans;

namespace IntoChat.OS.Modules.Flutter.Workspace;

// One server-sent stream of the signals a shell screen renders live: GET /brains/{id}/live?type=A&type=B.
// Each event is {"type","publisher","payload"}. A source's last retained signal of the type is sent first,
// so a screen opened late starts from the current state instead of waiting for the next change. Once the
// retained values of every source there at connect time are written, one {"type":"ready"} event follows:
// the client knows it is connected and the replay is complete.
internal static class LiveSignalEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    // Sources yields the runtime ids of the neurons that publish the [Signal] record: those that exist now, then any that appear
    // later. It calls listed once the ones that exist now are yielded; ending the sequence counts as listed too. Live is a test
    // seam that replaces a source's session subscription; nothing in the product passes it.
    internal sealed record LiveFeed(
        string Type,
        Type Signal,
        Func<BrainEdge, string, Action, CancellationToken, IAsyncEnumerable<DigitalBrain.NeuronId>> Sources)
    {
        public Func<DigitalBrain.NeuronId, Func<Task>, CancellationToken, IAsyncEnumerable<SessionSignal>>? Live { get; init; }
    }

    internal static LiveFeed Feed<T>(
        Func<BrainEdge, string, Action, CancellationToken, IAsyncEnumerable<DigitalBrain.NeuronId>> sources) where T : DigitalBrain.Signal
        => new(typeof(T).Name, typeof(T), sources);

    internal static readonly LiveFeed[] Feeds =
    [
        // The deployment is a plain neuron (06-08).
        Feed<DeploymentChanged>(static (_, brainId, _, _) => SingleId(new(new DigitalBrain.BrainId(brainId), "aspire.deployment", IDeployment.Relative))),
        // Apps are plain neurons (06-08): their activity is watched on the session synapse neuron.
        Feed<AppActivity>(Apps),
        // R126: asked of a circle brain's own /live; the shell watches each circle the person is in. Circles are plain (06-08).
        Feed<CircleChanged>(static (_, brainId, _, _) => SingleId(new(new DigitalBrain.BrainId(brainId), IntoChat.OS.Connections.CircleGrainType.Name, "circle"))),
        Feed<CircleSpoke>(static (_, brainId, _, _) => SingleId(new(new DigitalBrain.BrainId(brainId), "circles.chat", "chat"))),
    ];

    private static async IAsyncEnumerable<DigitalBrain.NeuronId> SingleId(DigitalBrain.NeuronId id) { await Task.CompletedTask; yield return id; }

    // App keys waiting for the stream. A dropped key would leave an app out of the watch for good, so a full
    // buffer holds the kernel enumeration back instead of dropping.
    internal const int KeyBuffer = 256;

    // Apps already there, then each one the kernel announces: the watch is registered before the listing, so none is missed.
    // A null key marks the end of the listing. A key waiting in the buffer is not queued again, so the dedup set
    // holds at most the buffer's keys; once read, `seen` keeps the stream from yielding it twice.
    private static async IAsyncEnumerable<DigitalBrain.NeuronId> Apps(BrainEdge brain, string brainId, Action listed, [EnumeratorCancellation] CancellationToken ct)
    {
        var keys = Channel.CreateBounded<string?>(new BoundedChannelOptions(KeyBuffer) { FullMode = BoundedChannelFullMode.Wait });
        HashSet<string> queued = [];
        async Task Queue(string key)
        {
            lock (queued) { if (!queued.Add(key)) { return; } }
            await keys.Writer.WriteAsync(key, ct);
        }
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var created in brain.On<InstanceCreated>(brain.Self(brainId), async () =>
                {
                    await foreach (var instance in brain.Kernel.Instances<IApp>(ct)) { await Queue(instance.Key); }
                    await keys.Writer.WriteAsync(null, ct);
                }, ct)) { await Queue(created.Key); }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { keys.Writer.TryComplete(error); }
        }, CancellationToken.None);
        HashSet<string> seen = [];
        await foreach (var key in keys.Reader.ReadAllAsync(ct))
        {
            if (key is not null) { lock (queued) { queued.Remove(key); } }
            if (key is null) { listed(); }
            else if (IApp.NameOf(brainId, key) is not null && seen.Add(key))
            { yield return new(new DigitalBrain.BrainId(brainId), IntoChat.OS.Connections.AppGrainType.Name, key[(brainId.Length + 1)..]); }
        }
    }

    public static void Map(IEndpointRouteBuilder endpoints)
        => BrainRoutes.Group(endpoints).MapGet("/live", Live);

    private static async Task Live(HttpContext http, ILoggerFactory logs, CancellationToken ct)
    {
        var brain = BrainEdge.Open(http.RequestServices, BrainScope.CurrentId());
        var (selected, error) = Select(http.Request.Query["type"].Where(name => !string.IsNullOrEmpty(name)).Select(name => name!));
        if (error is not null)
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsJsonAsync(new { error }, ct);
            return;
        }
        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        await http.Response.Body.FlushAsync(ct);
        await foreach (var envelope in Stream(brain, BrainScope.CurrentId(), selected, logs.CreateLogger("IntoChat.OS.Modules.Flutter.Live"), ct, services: http.RequestServices))
        {
            await http.Response.WriteAsync("data: " + envelope + "\n\n", ct);
            await http.Response.Body.FlushAsync(ct);
        }
    }

    internal static (LiveFeed[] Feeds, string? Error) Select(IEnumerable<string> types)
    {
        var requested = types.Distinct().ToArray();
        var unknown = requested.Where(name => Feeds.All(feed => feed.Type != name)).ToArray();
        if (requested.Length == 0) { return ([], "Name at least one signal type."); }
        if (unknown.Length > 0) { return ([], $"Not a live signal: {string.Join(", ", unknown)}."); }
        return ([.. Feeds.Where(feed => requested.Contains(feed.Type))], null);
    }

    internal const string Ready = "{\"type\":\"ready\"}";

    // Sent instead of what a stalled reader would have missed; the stream then ends and the client reconnects and reloads.
    internal const string Resync = "{\"type\":\"resync\"}";

    // Envelopes held for a reader that is not keeping up; one more means resync.
    internal const int EnvelopeBuffer = 1024;

    // A bounded outbox that never blocks a pump: an envelope that does not fit marks the stream overflowed and ends it.
    private sealed class Outbox(int capacity)
    {
        private readonly Channel<string> _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.Wait });
        private int _overflowed;

        public bool Overflowed => Volatile.Read(ref _overflowed) == 1;
        public ChannelReader<string> Reader => _channel.Reader;

        public ValueTask Write(string envelope, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (_channel.Writer.TryWrite(envelope) || Overflowed) { return ValueTask.CompletedTask; }
            if (Interlocked.Exchange(ref _overflowed, 1) == 0) { _channel.Writer.TryComplete(); }
            return ValueTask.CompletedTask;
        }

        public void Fail(Exception error) => _channel.Writer.TryComplete(error);
    }

    // The envelopes as JSON text, retained signals first per source, then Ready, then live ones, until ct ends the stream.
    // The stream stays open with no sources at all: a source may appear later. A source or feed that fails is logged and dropped;
    // when every feed has failed the stream ends with an error, so the client sees a failure instead of a silent hang.
    // A reader that falls more than `capacity` envelopes behind gets Resync and the stream ends.
    internal static async IAsyncEnumerable<string> Stream(BrainEdge brain, string brainId, LiveFeed[] feeds, ILogger log, [EnumeratorCancellation] CancellationToken ct, int capacity = EnvelopeBuffer,
        IServiceProvider? services = null)
    {
        var outbox = new Outbox(capacity);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            var failed = 0;
            void Failed()
            {
                if (Interlocked.Increment(ref failed) == feeds.Length)
                { outbox.Fail(new InvalidOperationException("Every live feed failed; see the server log.")); }
            }
            var replayed = feeds.Select(feed => Task.Run(() => Discover(brain, brainId, feed, services, outbox, log, Failed, stop.Token), CancellationToken.None)).ToArray();
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.WhenAll(replayed);
                    await outbox.Write(Ready, stop.Token);
                }
                catch (Exception closed) when (closed is OperationCanceledException or ChannelClosedException) { log.LogSwallowed(closed, "live stream closed before ready"); }
            }, CancellationToken.None);
            while (await outbox.Reader.WaitToReadAsync(ct))
            {
                if (outbox.Overflowed) { break; }
                if (outbox.Reader.TryRead(out var envelope)) { yield return envelope; }
            }
            if (outbox.Overflowed)
            {
                log.LogInformation("Live stream for {Brain} overflowed {Capacity} envelopes; the reader resyncs.", brainId, capacity);
                yield return Resync;
            }
        }
        finally { await stop.CancelAsync(); }
    }

    // Completes once the retained values of the sources listed at connect time are written (or the feed failed); discovery keeps
    // running after that. Each source is watched on its own session.
    private static Task Discover(BrainEdge brain, string brainId, LiveFeed feed, IServiceProvider? services, Outbox writer, ILogger log, Action failed, CancellationToken ct)
    {
        var type = feed.Type;
        var replayed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (services is null && feed.Live is null)
        {
            log.LogWarning("Live {Type} feed dropped: this host registers no neuron services.", type);
            failed();
            replayed.TrySetResult();
            return replayed.Task;
        }
        var alias = SignalLog.Alias(feed.Signal);
        List<Task> initial = [];
        void Listed()
        {
            Task[] pending;
            lock (initial) { if (replayed.Task.IsCompleted) { return; } pending = [.. initial]; }
            _ = Task.WhenAll(pending).ContinueWith(_ => replayed.TrySetResult(), TaskScheduler.Default);
        }
        _ = Task.Run(async () =>
        {
            var listing = true;
            try
            {
                await foreach (var source in feed.Sources(brain, brainId, () => { listing = false; Listed(); }, ct))
                {
                    var replay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    if (listing) { lock (initial) { initial.Add(replay.Task); } }
                    _ = Task.Run(() => Pump(services, feed, alias, source, writer, log, replay, ct), CancellationToken.None);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { log.LogWarning(error, "Live {Type} feed dropped: its sources failed.", type); failed(); }
            finally { if (listing) { listing = false; Listed(); } }
        }, CancellationToken.None);
        return replayed.Task;
    }

    private static async Task Pump(IServiceProvider? services, LiveFeed feed, string alias, DigitalBrain.NeuronId source, Outbox writer, ILogger log, TaskCompletionSource replayed, CancellationToken ct)
    {
        var signal = feed.Signal;
        var journal = services?.GetService<IBrainJournalWriter>();
        try
        {
            async Task Replay()
            {
                try
                {
                    if (journal is not null && await SignalLog.Newest(journal, source, alias, ct) is { } last) { await writer.Write(PlainEnvelope(signal, source, last, replay: true), ct); }
                }
                finally { replayed.TrySetResult(); }
            }
            var live = feed.Live is { } seam ? seam(source, Replay, ct)
                : SignalLog.Live(services!, source, [alias], "live/" + Guid.NewGuid().ToString("N"), Replay, ct);
            await foreach (var entry in live)
            {
                if (!SignalLog.IsGap(entry) && entry.Alias == alias) { await writer.Write(PlainEnvelope(signal, source, entry), ct); }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { log.LogWarning(error, "Live {Type} source {Source} dropped.", signal.Name, source); }
        finally { replayed.TrySetResult(); }
    }

    internal static string PlainEnvelope(Type signal, DigitalBrain.NeuronId source, SessionSignal entry, bool replay = false) => JsonSerializer.Serialize(
        new
        {
            type = signal.Name,
            publisher = source.ToString(),
            sequence = (long?)entry.Sequence,
            replay = replay ? true : (bool?)null,
            payload = JsonSerializer.SerializeToElement(JsonSerializer.Deserialize(entry.Payload.GetRawText(), signal, JsonSerializerOptions.Web), signal, Json),
        }, Json);
}
