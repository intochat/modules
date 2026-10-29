using System.Collections.Concurrent;
using IntoChat.OS.Modules.AI.Models;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Connections.Platform.Connections;
using IntoChat.OS.Connections.Platform.Entries;
using IntoChat.OS.Connections.Platform.Integrations;

namespace IntoChat.OS.Modules.AI;

// Whether a brain holds its own key for a provider, cached per (brain, integration) and revalidated
// against the brain's entry version, so a Put or Remove takes effect on the next check. Only the
// boolean is cached; the key itself is released per client build and never kept.
internal sealed class BrainKeyAvailability(IGrainFactory grains, IAiCredentials credentials)
{
    private readonly ConcurrentDictionary<(string Brain, string Integration), (bool HasKey, long Version)> _known = new();

    public async Task<(bool HasKey, long Version)> Read(string brainId, AiProvider provider, CancellationToken ct)
    {
        var version = await grains.GetGrain<IBrainEntries>(brainId).Version().WaitAsync(ct).ConfigureAwait(false);
        var slot = (brainId, AiIntegrations.IdOf(provider));
        if (_known.TryGetValue(slot, out var known) && known.Version == version) { return known; }
        var fresh = (await credentials.BrainKey(brainId, provider, ct).ConfigureAwait(false) is { Length: > 0 }, version);
        _known[slot] = fresh;
        return fresh;
    }
}

// The deployment's credentials as one brain sees them, for availability only: a key-based provider
// is ready when the brain holds its own provider:{id} key. Materializing always returns the
// deployment's registration; a brain key is released only by ModelCatalog when it builds a client,
// and is then sent only to the provider's public endpoint (ProviderPublicEndpoints), never to an
// operator-declared, profile or registration endpoint.
internal sealed class BrainAwareCredentials(IAiCredentials deployment, BrainKeyAvailability keys, string brainId) : IAiCredentials
{
    public async Task<IntegrationSnapshot> StatusOf(string integrationId, CancellationToken cancellationToken = default)
        => Keyed(integrationId) is { } provider && (await keys.Read(brainId, provider, cancellationToken).ConfigureAwait(false)).HasKey
            ? Ready(integrationId)
            : await deployment.StatusOf(integrationId, cancellationToken).ConfigureAwait(false);

    public async Task<long> GenerationOf(string integrationId, CancellationToken cancellationToken = default)
    {
        var generation = await deployment.GenerationOf(integrationId, cancellationToken).ConfigureAwait(false);
        if (Keyed(integrationId) is not { } provider) { return generation; }
        var (hasKey, version) = await keys.Read(brainId, provider, cancellationToken).ConfigureAwait(false);
        return unchecked((generation * 1_000_003) + (version * 2) + (hasKey ? 1 : 0));
    }

    public Task<ReleasedIntegration> MaterializeAsync(string integrationId, CancellationToken cancellationToken = default)
        => deployment.MaterializeAsync(integrationId, cancellationToken);

    public Task<string> ReleaseSecretAsync(string integrationId, string field, CancellationToken cancellationToken = default)
        => deployment.ReleaseSecretAsync(integrationId, field, cancellationToken);

    public Task<string?> BrainKey(string brain, AiProvider provider, CancellationToken cancellationToken = default)
        => deployment.BrainKey(brain, provider, cancellationToken);

    private static AiProvider? Keyed(string integrationId)
        => AiIntegrations.KeyedProviders.Cast<AiProvider?>().FirstOrDefault(provider => AiIntegrations.IdOf(provider!.Value) == integrationId);

    private static IntegrationSnapshot Ready(string integrationId) => new() { IntegrationId = integrationId, Status = IntegrationStatus.Ready };
}

// The provider's own public endpoint, the only place a brain key is sent. A service so a host can
// point it at a stand-in; production uses the published defaults.
internal class ProviderPublicEndpoints
{
    public virtual string Of(AiProvider provider) => AiIntegrations.DefaultEndpointOf(provider);
}

// One brain key for one client build, with no settings: the factory then has no registration
// endpoint to fall back to.
internal sealed class BrainKeyCredentials(string integrationId, string key) : IAiCredentials
{
    private IntegrationSnapshot Snapshot => new() { IntegrationId = integrationId, Status = IntegrationStatus.Ready };

    public Task<IntegrationSnapshot> StatusOf(string id, CancellationToken cancellationToken = default) => Task.FromResult(Only(id));

    public Task<long> GenerationOf(string id, CancellationToken cancellationToken = default) { Only(id); return Task.FromResult(0L); }

    public Task<ReleasedIntegration> MaterializeAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(new ReleasedIntegration { Snapshot = Only(id), Values = new() { [AiIntegrations.ApiKeyField] = key } });

    public async Task<string> ReleaseSecretAsync(string id, string field, CancellationToken cancellationToken = default)
        => (await MaterializeAsync(id, cancellationToken).ConfigureAwait(false)).Values[field];

    public Task<string?> BrainKey(string brainId, AiProvider provider, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

    private IntegrationSnapshot Only(string id)
        => id == integrationId ? Snapshot : throw new InvalidOperationException("A brain key credential serves only its own provider.");
}
