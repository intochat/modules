namespace IntoChat.OS.Modules.Microsoft.Azure;

// The seam over Azure Resource Manager: the neuron and the installer talk to this, tests record against it.
public interface IAzureArm
{
    Task<ResourceRef> Ensure(string subscription, ResourceSpec spec, CancellationToken ct);

    // A secret value from the data plane of the vault; null when the secret does not exist.
    Task<string?> ReadSecret(string vault, string name, CancellationToken ct);

    Task<ResourceRef?> Read(string subscription, ResourceRef resource, CancellationToken ct);

    Task Remove(string subscription, ResourceRef resource, CancellationToken ct);

    Task<ResourceRef[]> List(string subscription, string tagValue, CancellationToken ct);
}
