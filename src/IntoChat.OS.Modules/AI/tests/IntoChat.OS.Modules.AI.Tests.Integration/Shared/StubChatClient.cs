using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

namespace IntoChat.OS.Modules.AI.Tests.Integration;

internal delegate Task<ChatResponse> ChatResponder(IReadOnlyList<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken);

internal delegate IAsyncEnumerable<ChatResponseUpdate> ChatStreamer(IReadOnlyList<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken);

internal sealed class StubChatClient(ChatResponder? respond = null, ChatStreamer? stream = null) : IChatClient
{
    private readonly ConcurrentQueue<ChatMessage[]> requests = new();

    public IReadOnlyList<ChatMessage[]> Requests => [.. requests];

    public static StubChatClient Replying(string answer) =>
        new((_, _, _) => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer))));

    public static StubChatClient StreamingOnly(ChatStreamer stream) => new(stream: stream);

    public static StubChatClient Failing(string message) => new((_, _, _) => throw new InvalidOperationException(message));

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var recorded = Record(messages);
        return respond is null ? throw new NotSupportedException() : respond(recorded, options, cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var recorded = Record(messages);
        if (stream is not null) { return stream(recorded, options, cancellationToken); }
        return respond is null ? NoUpdates() : Streamed(() => respond(recorded, options, cancellationToken));
    }

    // The agent always streams; a stub with only a responder streams that response.
    private static async IAsyncEnumerable<ChatResponseUpdate> Streamed(Func<Task<ChatResponse>> respond)
    {
        foreach (var update in (await respond()).ToChatResponseUpdates()) { yield return update; }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }

    private ChatMessage[] Record(IEnumerable<ChatMessage> messages)
    {
        var recorded = messages.ToArray();
        requests.Enqueue(recorded);
        return recorded;
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> NoUpdates()
    {
        await Task.CompletedTask;
        yield break;
    }
}
