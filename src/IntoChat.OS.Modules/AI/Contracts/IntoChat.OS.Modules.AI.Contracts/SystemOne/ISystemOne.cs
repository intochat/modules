using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.SystemOne;

// A System One model (TypeSafe Jev): state in, typed decisions out. Not a chat model: it never
// generates strings, so it cannot be asked through ILLM. It decides, and code branches on the answer.
[NeuronAlias("ai.systemone"), DefaultNeuronType("ai.systemone")]
public interface ISystemOne
{
    // One System One neuron per brain (R4-19: a brain-scoped singleton is keyed main).
    static string Key(string brainId) => $"{brainId}/main";

    [DigitalBrain.Interleaves] Task<SystemOneResult> Ask(SystemOneRequest request, CancellationToken ct = default);
}
