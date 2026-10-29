using IntoChat.OS.Modules.AI.Inference;

namespace IntoChat.OS.Modules.AI.Models;

// What a model neuron asks of inference (06-05): the AI module binds InferenceService; a both-runtimes fact binds a fake, so the
// real model neurons (their scheduling, signals and admission) run on TestOS as on the silo.
internal interface IModelInference
{
    bool HasProfile(string name);

    Task<ModelDescriptor> Describe(ModelSelection? selection, Type? marker, CancellationToken cancellationToken, string? brainId);

    Task<InferenceResult> Generate(InferenceRequest request, Type? marker, CancellationToken cancellationToken, string? brainId);

    IAsyncEnumerable<InferenceUpdate> GenerateStreaming(InferenceRequest request, Type? marker, CancellationToken cancellationToken, string? brainId);
}
