using System.Collections.Concurrent;
using IntoChat.OS;
using IntoChat.OS.Modules.AI.Models;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Connections.Platform.Entries;
using IntoChat.OS.Connections.Platform.Integrations;

namespace IntoChat.OS.Modules.AI;

internal interface IAiCredentials
{
    Task<IntegrationSnapshot> StatusOf(string integrationId, CancellationToken cancellationToken = default);
    Task<long> GenerationOf(string integrationId, CancellationToken cancellationToken = default);
    Task<ReleasedIntegration> MaterializeAsync(string integrationId, CancellationToken cancellationToken = default);
    Task<string> ReleaseSecretAsync(string integrationId, string field, CancellationToken cancellationToken = default);

    // The brain's own key for a key-based provider (its provider:{id} secret entry), or null. The
    // value is for the provider call being built; callers must not log, cache or return it.
    Task<string?> BrainKey(string brainId, AiProvider provider, CancellationToken cancellationToken = default);
}

internal sealed class RegistrationCredentials(IModuleIntegrations<AIModule> integrations, TimeProvider time, IBrainProviderKeys? brainKeys = null) : IAiCredentials
{
    public async Task<string?> BrainKey(string brainId, AiProvider provider, CancellationToken cancellationToken = default)
    {
        if (brainKeys is null || !BrainScope.IsValidId(brainId) || !AiIntegrations.KeyedProviders.Contains(provider)) { return null; }
        try { return await brainKeys.Release(brainId, AiIntegrations.IdOf(provider), cancellationToken).ConfigureAwait(false); }
        catch (ArgumentException) { return null; }
    }

    private static readonly TimeSpan StatusLifetime = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, (IntegrationSnapshot Snapshot, DateTimeOffset ReadAt, long Generation)> _statuses = new(StringComparer.Ordinal);
    private long _generation;

    public async Task<long> GenerationOf(string integrationId, CancellationToken cancellationToken = default)
    {
        await StatusOf(integrationId, cancellationToken).ConfigureAwait(false);
        return _statuses[integrationId].Generation;
    }

    public async Task<ReleasedIntegration> MaterializeAsync(string integrationId, CancellationToken cancellationToken = default)
    {
        var handle = integrations.Get(integrationId);
        var snapshot = await handle.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.Status != IntegrationStatus.Ready)
        {
            throw new ProviderUnavailableException(integrationId, snapshot.Status.ToString(), snapshot.MissingFields);
        }
        return await handle.ReleaseAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ReleaseSecretAsync(string integrationId, string field, CancellationToken cancellationToken = default)
        => (await MaterializeAsync(integrationId, cancellationToken).ConfigureAwait(false)).Values[field];

    private static bool SameRegistration(IntegrationSnapshot before, IntegrationSnapshot after)
        => before.Status == after.Status && before.Revision == after.Revision
            && before.MissingFields.SequenceEqual(after.MissingFields, StringComparer.Ordinal)
            && before.Settings.Count == after.Settings.Count
            && before.Settings.All(pair => after.Settings.TryGetValue(pair.Key, out var value) && value == pair.Value);

    public async Task<IntegrationSnapshot> StatusOf(string integrationId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = time.GetUtcNow();
            if (_statuses.TryGetValue(integrationId, out var cached) && now - cached.ReadAt < StatusLifetime) { return cached.Snapshot; }
            var snapshot = await integrations.Get(integrationId).ReadAsync(cancellationToken).ConfigureAwait(false);
            var generation = _statuses.TryGetValue(integrationId, out var previous) && SameRegistration(previous.Snapshot, snapshot)
                ? previous.Generation : Interlocked.Increment(ref _generation);
            _statuses[integrationId] = (snapshot, now, generation);
            return snapshot;
        }
        finally { _gate.Release(); }
    }
}

// Test seam for hosts that build provider selection without a brain: fixed readiness, endpoints and keys.
internal sealed class FixedAiCredentials : IAiCredentials
{
    private readonly Dictionary<string, (string ApiKey, string? Endpoint)> _ready = new(StringComparer.Ordinal);

    public FixedAiCredentials Ready(string integrationId, string apiKey, string? endpoint = null)
    {
        _ready[integrationId] = (apiKey, endpoint);
        return this;
    }

    public Task<long> GenerationOf(string integrationId, CancellationToken cancellationToken = default) => Task.FromResult(0L);

    public Task<string?> BrainKey(string brainId, AiProvider provider, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

    public Task<IntegrationSnapshot> StatusOf(string integrationId, CancellationToken cancellationToken = default)
        => Task.FromResult(_ready.TryGetValue(integrationId, out var entry)
            ? new IntegrationSnapshot
            {
                IntegrationId = integrationId,
                Status = IntegrationStatus.Ready,
                Settings = entry.Endpoint is null ? [] : new() { [AiIntegrations.EndpointField] = entry.Endpoint },
            }
            : new IntegrationSnapshot { IntegrationId = integrationId, Status = IntegrationStatus.Unconfigured, MissingFields = [AiIntegrations.ApiKeyField] });

    public async Task<ReleasedIntegration> MaterializeAsync(string integrationId, CancellationToken cancellationToken = default)
    {
        if (!_ready.TryGetValue(integrationId, out var entry)) { throw new ProviderUnavailableException(integrationId, IntegrationStatus.Unconfigured.ToString(), [AiIntegrations.ApiKeyField]); }
        return new ReleasedIntegration { Snapshot = await StatusOf(integrationId, cancellationToken), Values = new() { [AiIntegrations.ApiKeyField] = entry.ApiKey } };
    }

    public async Task<string> ReleaseSecretAsync(string integrationId, string field, CancellationToken cancellationToken = default)
        => (await MaterializeAsync(integrationId, cancellationToken)).Values[field];

}

internal static class AiCredentialsExtensions
{
    internal static async Task<bool> IsReady(this IAiCredentials credentials, string integrationId, CancellationToken cancellationToken = default)
        => (await credentials.StatusOf(integrationId, cancellationToken).ConfigureAwait(false)).Status == IntegrationStatus.Ready;

    internal static async Task<string?> Setting(this IAiCredentials credentials, string integrationId, string field, CancellationToken cancellationToken = default)
        => (await credentials.StatusOf(integrationId, cancellationToken).ConfigureAwait(false)).Settings.TryGetValue(field, out var value) ? value : null;

    internal static async Task RequireReady(this IAiCredentials credentials, string integrationId, CancellationToken cancellationToken = default)
    {
        var snapshot = await credentials.StatusOf(integrationId, cancellationToken).ConfigureAwait(false);
        if (snapshot.Status != IntegrationStatus.Ready)
        {
            throw new ProviderUnavailableException(integrationId, snapshot.Status.ToString(), snapshot.MissingFields);
        }
    }
}
