namespace IntoChat.OS.Modules.AI.Models;

// Turns an LLM marker name used in configuration, such as "IGpt56Luna", into its neuron inside a brain.
// ILLM serves every caller as its own brain, but its key still belongs to one brain like every neuron key:
// the inference signals it publishes journal into that brain, and a machine refuses a key outside every brain.
public static class ModelAddress
{
    // A brain's model neuron: the deployment's default model for the marker it is resolved as.
    public static string Key(string brainId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brainId);
        if (brainId.Contains('/')) { throw new ArgumentException($"'{brainId}' is a neuron key, not a brain id.", nameof(brainId)); }
        return $"{brainId}/models";
    }

    public static bool IsKnown(string model) => LLMModel.FindByMarkerName(model) is not null;
}
