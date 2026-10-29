namespace IntoChat.OS.Modules.Microsoft.Aspire.Deployment;

// Roll hands each step to this one-way self-reference: the caller's request ends at once, and every step persists
// before and after its side effect, so an activation that dies mid-roll resumes at the step it was on.
// A plain contract of the deployment neuron (06-08).
[DigitalBrain.Contracts.NeuronAlias("aspire.deployment-driver"), DigitalBrain.Contracts.DefaultNeuronType("aspire.deployment")]
internal interface IDeploymentDriver
{
    [DigitalBrain.OneWayCall] Task Step();
    [DigitalBrain.Interleaves]
    Task MembershipChanged(Guid activation, IntoChat.OS.Modules.Microsoft.Orleans.ClusterSnapshot? snapshot);
}
