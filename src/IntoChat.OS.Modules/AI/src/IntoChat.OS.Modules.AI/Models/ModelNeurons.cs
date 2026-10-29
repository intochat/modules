using IntoChat.OS.Modules.AI.Inference;
using DigitalBrain.Runtime.Orleans.Hosting;

namespace IntoChat.OS.Modules.AI.Models;

// Model neurons are reached by their marker contract and the brain's model key. The contracts assembly
// cannot name the silo host, so callers outside that assembly use this.
public static class ModelNeurons
{
    public static ILLM Resolve(IServiceProvider neurons, string brainId, string model)
    {
        ArgumentNullException.ThrowIfNull(neurons);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        var marker = LLMModel.FindByMarkerName(model)?.Marker ?? throw new ArgumentException($"Unknown model '{model}'.", nameof(model));
        return (ILLM)SiloNeurons.Get(neurons, marker, ModelAddress.Key(brainId));
    }

    public static async Task<string> Complete(IServiceProvider neurons, string brainId, string model, string system, string user, CancellationToken cancellationToken = default)
    {
        var result = await Resolve(neurons, brainId, model).Generate(new InferenceRequest(
        [
            new AiMessage("system", [new AiText(system)]),
            new AiMessage("user", [new AiText(user)]),
        ]), cancellationToken);
        return string.Concat(result.Messages.SelectMany(message => message.Content).OfType<AiText>().Select(content => content.Text)).Trim();
    }
}
