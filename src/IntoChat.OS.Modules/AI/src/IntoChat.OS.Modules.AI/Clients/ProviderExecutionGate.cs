using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.AI;

// Shared by every provider client in one host; changing model/profile does not create another budget.
internal sealed class ProviderExecutionGate : IDisposable
{
    private readonly SemaphoreSlim slots;
    private readonly AIHostingOptions limits;

    public ProviderExecutionGate(IOptions<AIOptions> options)
    {
        limits = options.Value.Hosting;
        if (limits.MaxConcurrentRequests is < 1 or > 1024 || limits.MaxExecutionSeconds is < 1 or > 3600 || limits.MaxOutputTokens is < 1 or > 1_000_000)
        { throw new InvalidOperationException("Provider execution limits must be positive and bounded."); }
        var concurrency = limits.MaxConcurrentRequests ?? int.MaxValue;
        slots = new SemaphoreSlim(concurrency, concurrency);
    }

    internal Lease Enter(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!slots.Wait(0, cancellationToken)) { throw new InvalidOperationException("Provider concurrency limit reached."); }
        return new Lease(slots, limits.MaxExecutionSeconds, cancellationToken);
    }

    internal ChatOptions? Bound(ChatOptions? options)
    {
        if (limits.MaxOutputTokens is not { } maximum) { return options; }
        if (options?.MaxOutputTokens > maximum) { throw new InvalidOperationException("Provider output limit exceeded."); }
        var bounded = options?.Clone() ?? new ChatOptions();
        bounded.MaxOutputTokens ??= maximum;
        return bounded;
    }

    public void Dispose() => slots.Dispose();

    internal sealed class Lease : IDisposable
    {
        private readonly SemaphoreSlim slots;
        private readonly CancellationTokenSource deadline;
        internal Lease(SemaphoreSlim slots, int? seconds, CancellationToken ct)
        {
            this.slots = slots;
            deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (seconds is { } duration) { deadline.CancelAfter(TimeSpan.FromSeconds(duration)); }
        }
        internal CancellationToken Token => deadline.Token;
        public void Dispose() { deadline.Dispose(); slots.Release(); }
    }
}

internal sealed class LimitedChatClient(IChatClient inner, ProviderExecutionGate gate) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        options = gate.Bound(options);
        using var lease = gate.Enter(cancellationToken);
        // Keep the slot until the actual provider call exits, including a slow cancellation.
        return await base.GetResponseAsync(messages, options, lease.Token).ConfigureAwait(false);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options = gate.Bound(options);
        using var lease = gate.Enter(cancellationToken);
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, lease.Token).ConfigureAwait(false))
        { yield return update; }
    }
}

internal sealed class LimitedEmbeddingGenerator(IEmbeddingGenerator<string, Embedding<float>> inner, ProviderExecutionGate gate)
    : DelegatingEmbeddingGenerator<string, Embedding<float>>(inner)
{
    public override async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var lease = gate.Enter(cancellationToken);
        return await base.GenerateAsync(values, options, lease.Token).ConfigureAwait(false);
    }
}
