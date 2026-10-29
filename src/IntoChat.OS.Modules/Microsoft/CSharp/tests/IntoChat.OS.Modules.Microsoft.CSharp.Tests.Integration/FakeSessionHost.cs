using System.Collections.Concurrent;
using System.Net;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

// Stands in for the sandbox host's session API: a start records the session, a stop forgets it, and a
// test can lose every session as a replaced container would. The brain talks to it off its own turn,
// so facts await the requests they expect instead of reading them back at once.
internal sealed class FakeSessionHost : HttpMessageHandler
{
    public const string Url = "http://sandbox.test/";

    private readonly Channel<(string Method, string Path, JsonNode? Body)> _seen = Channel.CreateUnbounded<(string, string, JsonNode?)>();

    public ConcurrentDictionary<string, JsonNode> Sessions { get; } = new(StringComparer.Ordinal);
    public ConcurrentQueue<(string Method, string Path, JsonNode? Body)> Requests { get; } = new();
    // Nothing listens: every request fails to connect, as when the sandbox container is not up yet.
    public bool Absent { get; set; }
    // A proxy that accepts the connection for a backend that is gone: no answer, ever.
    public bool Hang { get; set; }
    // Holds every stop until released, so a fact can see what reaches the host while a stop is in flight.
    public TaskCompletionSource? HoldStops { get; set; }
    // A start arrived while a stop was still in flight: the two can land in either order.
    public bool StartDuringStop { get; private set; }

    private int _stopping;

    public async Task<(string Method, string Path, JsonNode? Body)> NextAsync(Func<(string Method, string Path, JsonNode? Body), bool> match, CancellationToken ct)
    {
        while (true)
        {
            var request = await _seen.Reader.ReadAsync(ct);
            if (match(request)) { return request; }
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Absent) { throw new HttpRequestException("No connection could be made because the target machine actively refused it."); }
        var path = request.RequestUri!.AbsolutePath.Trim('/');
        var text = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        var body = text.Length == 0 ? null : JsonNode.Parse(text);
        Requests.Enqueue((request.Method.Method, path, body));
        _seen.Writer.TryWrite((request.Method.Method, path, body));
        if (Hang) { await Task.Delay(Timeout.Infinite, cancellationToken); }
        if (IsStart((request.Method.Method, path, body)) && Volatile.Read(ref _stopping) > 0) { StartDuringStop = true; }
        if (HoldStops is { } hold && IsStop((request.Method.Method, path, body)))
        {
            Interlocked.Increment(ref _stopping);
            try { await hold.Task.WaitAsync(cancellationToken); }
            finally { Interlocked.Decrement(ref _stopping); }
        }
        return path.Split('/') switch
        {
            ["sessions", var id] when request.Method == HttpMethod.Post => Started(id, body!),
            ["sessions", var id] when Sessions.ContainsKey(id) => new(HttpStatusCode.OK),
            ["sessions", var id, "stop"] when Sessions.TryRemove(id, out _) => new(HttpStatusCode.OK),
            _ => new(HttpStatusCode.NotFound),
        };
    }

    public static bool IsStart((string Method, string Path, JsonNode? Body) request)
        => request.Method == "POST" && request.Path.Split('/') is ["sessions", _];

    public static bool IsStop((string Method, string Path, JsonNode? Body) request)
        => request.Method == "POST" && request.Path.EndsWith("/stop", StringComparison.Ordinal);

    private HttpResponseMessage Started(string id, JsonNode body)
    {
        Sessions[id] = body;
        return new(HttpStatusCode.Accepted);
    }
}
