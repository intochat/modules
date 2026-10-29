using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Connections.Platform.Connections;

namespace IntoChat.OS.Modules.Microsoft.Azure;

[GenerateSerializer, Alias("azure.connection-state")]
internal sealed class AzureConnectionState : ConnectionState
{
    [Id(0)] public AzureEnsured[] Ensured { get; set; } = [];
}

[GenerateSerializer, Alias("azure.ensured")]
internal sealed record AzureEnsured(
    [property: Id(0)] ResourceRef Resource,
    [property: Id(1)] string Env,
    [property: Id(2)] string Owner);

// The connection to one Azure subscription; its key names the subscription. It remembers what it ensured,
// so the inventory is readable without listing Azure.
[DigitalBrain.Neuron("azure", State = "azure")]
internal sealed class AzureNeuron(
    IAzureArm arm)
    : ConnectionNeuron<AzureConnectionState>, IAzure
{
    public async Task<ResourceRef> Ensure(ResourceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var tagged = AzureTags.Tagged(spec);
        var resource = await arm.Ensure(Subscription, tagged, CancellationToken.None);
        var entry = new AzureEnsured(resource, spec.Env, spec.Owner);
        var kept = Snapshot.Ensured.Where(e => e.Resource.Id != resource.Id).ToArray();
        Snapshot.Ensured = [.. kept, entry];
        await SaveQuietly(Snapshot);
        return resource;
    }

    public Task<ResourceRef?> Read(ResourceRef resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return arm.Read(Subscription, resource, CancellationToken.None);
    }

    public async Task Remove(ResourceRef resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        await arm.Remove(Subscription, resource, CancellationToken.None);
        Snapshot.Ensured = Snapshot.Ensured.Where(e => e.Resource.Id != resource.Id).ToArray();
        await SaveQuietly(Snapshot);
    }

    public async Task<ResourceRef[]> Inventory(string tagValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagValue);
        var known = Snapshot.Ensured.Where(e => e.Env == tagValue || e.Owner == tagValue).Select(e => e.Resource).ToArray();
        return known.Length > 0 ? known : await arm.List(Subscription, tagValue, CancellationToken.None);
    }

    protected override Task<ConnectionProbeResult> ProbeAsync(Credential credential, CancellationToken ct) =>
        Task.FromResult(ConnectionProbeResult.Connected());

    // Tests construct the neuron without a grain key; the key names the subscription once activated.
    internal string? SubscriptionOverride { get; set; }

    private string Subscription => SubscriptionOverride ?? Key.Connection;
}
