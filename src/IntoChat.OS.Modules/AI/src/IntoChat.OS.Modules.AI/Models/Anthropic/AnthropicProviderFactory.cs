using Anthropic;
using Microsoft.Extensions.AI;

namespace IntoChat.OS.Modules.AI.Models.Anthropic;

internal sealed class AnthropicProviderFactory : ApiKeyProviderFactory
{
    public override AiProvider Provider => AiProvider.Anthropic;

    public override async Task<IChatClient> CreateChatClient(string model, AIOptions configuration, IAiCredentials credentials, string? endpoint = null, CancellationToken cancellationToken = default)
    {
        var material = await credentials.MaterializeAsync(IntegrationId, cancellationToken).ConfigureAwait(false);
        return new AnthropicClient
        {
            ApiKey = material.Values[AiIntegrations.ApiKeyField],
            BaseUrl = EndpointOf(configuration, material.Snapshot, endpoint).OriginalString,
            Timeout = TimeSpan.FromMinutes(5),
        }.AsIChatClient(model);
    }

    public override Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGenerator(
        EmbeddingModel model,
        AIOptions configuration,
        IAiCredentials credentials, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Anthropic does not provide embedding models.");
}
