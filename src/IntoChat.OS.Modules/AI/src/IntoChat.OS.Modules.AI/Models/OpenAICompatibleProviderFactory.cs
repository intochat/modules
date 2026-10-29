using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace IntoChat.OS.Modules.AI.Models;

internal abstract class OpenAICompatibleProviderFactory : ApiKeyProviderFactory
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(5);

    public override async Task<IChatClient> CreateChatClient(string model, AIOptions configuration, IAiCredentials credentials, string? endpoint = null, CancellationToken cancellationToken = default)
    {
        var builder = new ChatClientBuilder(
            (await CreateClient(configuration, credentials, endpoint, cancellationToken)).GetChatClient(model).AsIChatClient());

        if (Provider is AiProvider.OpenAI && model.StartsWith("gpt-5.6", StringComparison.OrdinalIgnoreCase))
        {
            builder.ConfigureOptions(static options =>
            {
                // GPT-5.6 Chat Completions rejects function tools when reasoning
                // effort is omitted or enabled. Keep an explicit caller choice,
                // but use the API-supported "none" value otherwise.
                if (options.Tools is { Count: > 0 } && options.Reasoning is null)
                {
                    options.Reasoning = new ReasoningOptions { Effort = ReasoningEffort.None };
                }
            });
        }

        return builder
            .UseStreamingUsage()
            .Build();
    }

    public override async Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGenerator(
        EmbeddingModel model,
        AIOptions configuration,
        IAiCredentials credentials, CancellationToken cancellationToken = default)
        => (await CreateClient(configuration, credentials, null, cancellationToken)).GetEmbeddingClient(model.Id).AsIEmbeddingGenerator();

    private async Task<OpenAIClient> CreateClient(AIOptions configuration, IAiCredentials credentials, string? pinnedEndpoint, CancellationToken cancellationToken)
    {
        var material = await credentials.MaterializeAsync(IntegrationId, cancellationToken).ConfigureAwait(false);
        var options = new OpenAIClientOptions { NetworkTimeout = RequestTimeout, Endpoint = EndpointOf(configuration, material.Snapshot, pinnedEndpoint) };
        return new OpenAIClient(new ApiKeyCredential(material.Values[AiIntegrations.ApiKeyField]), options);
    }
}
