using DigitalBrain;

namespace IntoChat.OS.Modules.AI.Models;

// A brain's catalog is {brain}/ai.models/main; the deployment's view is the system brain's (R4-19).
[Neuron("ai.models"), StatelessNeuron]
internal sealed class ModelCatalogNeuron(ModelCatalog catalog) : DigitalBrain.Neuron, IModelCatalog
{
    private string? Brain => Id.Brain.Value == IModelCatalog.SystemBrain ? null : Id.Brain.Value;

    public Task<ModelChoices> List(string? defaultModel = null) => catalog.List(Brain, defaultModel, CancellationToken.None);
    public Task<ModelSelection?> Select(string? id, string? defaultModel = null) => catalog.Select(Brain, id, defaultModel, CancellationToken.None);
}
