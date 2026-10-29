using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS.Modules.AI.Inference;
using DigitalBrain.Access;

namespace IntoChat.OS.Modules.AI.Models;

[NeuronAlias("ai.llm"), DefaultNeuronType("ai.llm")]
// One model serves every brain; each caller is metered and granted as its own brain.
[DigitalBrain.Access.Shared]
public interface ILLM : IAiMarker
{
    Task<ModelDescriptor> Describe(ModelSelection? selection = null);
    [DigitalBrain.CallTimeout(600000)] Task<InferenceResult> Generate(InferenceRequest request, CancellationToken cancellationToken = default);
    [DigitalBrain.CallTimeout(600000)] IAsyncEnumerable<InferenceUpdate> GenerateStreaming(InferenceRequest request, CancellationToken cancellationToken = default);
}
