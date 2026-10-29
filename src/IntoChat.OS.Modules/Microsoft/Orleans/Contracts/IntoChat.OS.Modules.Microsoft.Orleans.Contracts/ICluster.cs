using DigitalBrain.Contracts;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Microsoft.Orleans;

public sealed record SiloView(
    string Name,
    string Address,
    string Status,
    string? Role,
    string Sha);

public sealed record ClusterSnapshot(SiloView[] Silos, long Version);

// The membership of the cluster this brain runs on, as Orleans sees it. A roll or restart is the Aspire
// module's act (IDeployment); this neuron only reports who is up.
[NeuronAlias("orleans.cluster"), DefaultNeuronType("orleans.cluster")]
public interface ICluster
{
    Task<ClusterSnapshot> Read();
    static string Key(string brainId) => $"{brainId}/orleans/cluster";
}
