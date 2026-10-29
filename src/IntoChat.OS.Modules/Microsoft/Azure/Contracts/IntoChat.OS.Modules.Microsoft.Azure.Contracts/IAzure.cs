using System.ComponentModel;

namespace IntoChat.OS.Modules.Microsoft.Azure;

// One Azure subscription, keyed {brain}/connection/azure/{subscriptionId}. Ensure is an idempotent PUT per kind.
[DigitalBrain.Contracts.NeuronAlias("azure"), DigitalBrain.Contracts.DefaultNeuronType("azure")]
public interface IAzure
{
    [Description("Create or update one Azure resource from its spec and return its reference; a second call with the same spec changes nothing.")]
    Task<ResourceRef> Ensure(ResourceSpec spec);

    [Description("Read one Azure resource, or nothing when it does not exist.")]
    Task<ResourceRef?> Read(ResourceRef resource);

    [Description("Delete one Azure resource.")]
    Task Remove(ResourceRef resource);

    [Description("The ensured resources whose env or owner tag equals the value.")]
    Task<ResourceRef[]> Inventory(string tagValue);
}

public sealed record ResourceRef(
    string Id,
    string Kind,
    string Name,
    string ResourceGroup)
{
    // The address a client uses: a registry's login server, a Postgres server's host name.
    public string Endpoint { get; init; } = "";
}
