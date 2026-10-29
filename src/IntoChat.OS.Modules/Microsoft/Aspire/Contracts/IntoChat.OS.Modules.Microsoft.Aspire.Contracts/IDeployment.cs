using IntoChat.OS;

namespace IntoChat.OS.Modules.Microsoft.Aspire;

public enum DeploymentStage { Steady, Restarting, Rolling, RollingBack }

public sealed record DeploymentSnapshot(
    string CurrentSha,
    DeploymentStage Stage,
    string? TargetSha,
    string[] Remaining,
    string? Failure);

// A plain neuron (06-08).
[DigitalBrain.Contracts.NeuronAlias("aspire.deployment"), DigitalBrain.Contracts.DefaultNeuronType("aspire.deployment")]
public interface IDeployment
{
    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves] Task<DeploymentSnapshot> Read();
    [DigitalBrain.CallTimeout(600_000)] Task<DeploymentSnapshot> Restart(string sha);
    // Instance by instance onto a compatible build: the MCP clients, then the silos, the one running the roll last.
    // Answers once the roll is under way; its end is DeploymentChanged(Steady): on sha, or back on the previous build
    // with the Failure that sent it back, which Read() reports. Callers follow a roll through Read()/DeploymentChanged and never resend Roll
    // while it runs: a step holds the cluster for up to two SiloStepBudgets, and a resend queued behind it outlives
    // the default response timeout. Refused unless the calling process is one of the cluster's instances.
    // Who is up is not this neuron's: the membership is IntoChat.OS.Modules.Microsoft.Orleans.ICluster.
    Task<DeploymentSnapshot> Roll(string sha);
    static string Key(string brainId) => $"{brainId}/{Relative}";
    // The id inside its brain.
    const string Relative = "aspire/deployment";
}
