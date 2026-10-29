using IntoChat.OS.Contracts.Compute;
using IntoChat.OS.Framework.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace IntoChat.OS.Modules.AI.Metering;

// Prices the provider's reported input tokens for embedding calls through the kernel's cost meter.
// Embeddings are their own row: the item carries the embedding model id, not a chat model's.
internal sealed class MeteringEmbeddingGenerator(
    IEmbeddingGenerator<string, Embedding<float>> innerGenerator, ICostMeter meter, Funding funding, string model, ILogger? logger = null)
    : DelegatingEmbeddingGenerator<string, Embedding<float>>(innerGenerator)
{
    public override async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var embeddings = await base.GenerateAsync(values, options, cancellationToken).ConfigureAwait(false);
        if (embeddings.Usage?.InputTokenCount is { } input)
        {
            var id = Guid.NewGuid().ToString("N");
            try
            {
                await meter.RecordAsync(CostKind.Model, $"{model}:input", input / 1_000_000m, "mtok", funding, $"{id}:input", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // Metering is an observer: a ledger fault must never fail the embedding call.
                logger?.LogSwallowed(exception, "embedding call metering failed");
            }
        }
        return embeddings;
    }
}
