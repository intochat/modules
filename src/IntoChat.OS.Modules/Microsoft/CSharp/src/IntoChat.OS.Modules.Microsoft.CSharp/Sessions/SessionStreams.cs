using System.Collections.Concurrent;
using System.Threading.Channels;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Sessions;

// The open stream of each connected session on this silo, so a brain runtime can push events to its
// host. Not grain state: a session reconnects to whichever silo serves it and is resent what is pending.
internal sealed class SessionStreams
{
    private readonly ConcurrentDictionary<string, Channel<(string Kind, object Payload)>> _open = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _logs = new(StringComparer.Ordinal);

    public Channel<(string Kind, object Payload)> Open(string session)
    {
        var channel = Channel.CreateUnbounded<(string, object)>(new() { SingleReader = true });
        if (_open.TryGetValue(session, out var previous)) { previous.Writer.TryComplete(); }
        _open[session] = channel;
        return channel;
    }

    public bool TryPush(string session, string kind, object payload)
        => _open.TryGetValue(session, out var channel) && channel.Writer.TryWrite((kind, payload));

    // A behavior's log tail lives in the host; the brain asks over the stream and the host answers with
    // a report. A host that does not answer within the cap leaves the logs empty.
    public async Task<string> RequestLogsAsync(string session, string behavior, int tail, TimeSpan cap)
    {
        var request = Guid.NewGuid().ToString("N");
        var answer = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _logs[request] = answer;
        try
        {
            if (!TryPush(session, "logs", new IntoChat.OS.Client.Runtime.V1.LogsRequestEvent(behavior, tail, request))) { return ""; }
            return await answer.Task.WaitAsync(cap).ConfigureAwait(false);
        }
        catch (TimeoutException) { return ""; }
        finally { _logs.TryRemove(request, out _); }
    }

    public void CompleteLogs(string request, string text)
    {
        if (_logs.TryRemove(request, out var answer)) { answer.TrySetResult(text); }
    }

    public void Close(string session, Channel<(string Kind, object Payload)> channel)
    {
        if (_open.TryRemove(new(session, channel))) { channel.Writer.TryComplete(); }
    }
}
