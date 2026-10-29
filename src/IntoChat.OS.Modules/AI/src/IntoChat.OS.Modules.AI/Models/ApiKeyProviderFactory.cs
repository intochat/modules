using IntoChat.OS.Connections.Platform.Integrations;
using Microsoft.Extensions.AI;

namespace IntoChat.OS.Modules.AI.Models;

internal abstract class ApiKeyProviderFactory : ILlmProviderFactory
{
    public abstract AiProvider Provider { get; }

    public Task<bool> IsConfigured(AIOptions configuration, IAiCredentials credentials, CancellationToken cancellationToken = default)
        => credentials.IsReady(IntegrationId, cancellationToken);

    public virtual Task<IChatClient> CreateChatClient(LLMModel model, AIOptions configuration, IAiCredentials credentials, CancellationToken cancellationToken = default)
        => CreateChatClient(model.Id, configuration, credentials, cancellationToken: cancellationToken);

    public abstract Task<IChatClient> CreateChatClient(string model, AIOptions configuration, IAiCredentials credentials, string? endpoint = null, CancellationToken cancellationToken = default);

    public abstract Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGenerator(
        EmbeddingModel model,
        AIOptions configuration,
        IAiCredentials credentials, CancellationToken cancellationToken = default);

    protected string IntegrationId => AiIntegrations.IdOf(Provider);

    protected Uri EndpointOf(AIOptions configuration, IntegrationSnapshot snapshot, string? pinned)
        => new(pinned is { Length: > 0 } ? pinned
            : snapshot.Settings.GetValueOrDefault(AiIntegrations.EndpointField)
            ?? DeclaredEndpoint(configuration)
            ?? AiIntegrations.DefaultEndpointOf(Provider));

    protected string? DeclaredEndpoint(AIOptions configuration)
        => configuration.Provider(Provider).Endpoint is { Length: > 0 } declared ? declared : null;
}
