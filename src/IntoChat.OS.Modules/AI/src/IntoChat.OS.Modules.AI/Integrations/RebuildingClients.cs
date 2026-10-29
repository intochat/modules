using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace IntoChat.OS.Modules.AI;

internal sealed class RebuildingChatClient(Func<CancellationToken, Task<IChatClient>> create, Func<CancellationToken, Task<long>> generation) : IChatClient
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChatClient? _inner;
    private long _builtAt = -1;

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => await (await Current(cancellationToken).ConfigureAwait(false)).GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await Current(cancellationToken).ConfigureAwait(false);
        await foreach (var update in client.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false)) { yield return update; }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() => _inner?.Dispose();

    private void RetireInner()
    {
        DeferredDisposal.After(_inner);
        _inner = null;
    }

    private async Task<IChatClient> Current(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = await generation(cancellationToken).ConfigureAwait(false);
            if (_inner is null || now != _builtAt)
            {
                RetireInner();
                _inner = await create(cancellationToken).ConfigureAwait(false);
                _builtAt = now;
            }

            return _inner;
        }
        finally { _gate.Release(); }
    }
}

internal sealed class RebuildingEmbeddingGenerator(Func<CancellationToken, Task<IEmbeddingGenerator<string, Embedding<float>>>> create, Func<CancellationToken, Task<long>> generation)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IEmbeddingGenerator<string, Embedding<float>>? _inner;
    private long _builtAt = -1;

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
        => await (await Current(cancellationToken).ConfigureAwait(false)).GenerateAsync(values, options, cancellationToken).ConfigureAwait(false);

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() => _inner?.Dispose();

    private void RetireInner()
    {
        DeferredDisposal.After(_inner);
        _inner = null;
    }

    private async Task<IEmbeddingGenerator<string, Embedding<float>>> Current(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = await generation(cancellationToken).ConfigureAwait(false);
            if (_inner is null || now != _builtAt)
            {
                RetireInner();
                _inner = await create(cancellationToken).ConfigureAwait(false);
                _builtAt = now;
            }

            return _inner;
        }
        finally { _gate.Release(); }
    }
}

// A replaced client may still be serving an in-flight call or stream, and some own their HttpClient; it is
// disposed after a grace longer than the provider request timeout instead of underneath that call.
internal static class DeferredDisposal
{
    internal static readonly TimeSpan Grace = TimeSpan.FromMinutes(6);

    internal static void After(IDisposable? retired)
    {
        if (retired is not null)
        {
            _ = Task.Delay(Grace).ContinueWith(_ => retired.Dispose(), TaskScheduler.Default);
        }
    }
}
