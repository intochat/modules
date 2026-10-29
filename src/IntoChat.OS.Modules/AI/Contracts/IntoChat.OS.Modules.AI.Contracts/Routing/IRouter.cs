using DigitalBrain.Contracts;

namespace IntoChat.OS.Modules.AI.Routing;

// A router picks a capability and method from the deployed contract catalog for unstructured
// state, using a System One model, then dispatches the call. It decides; it never generates prose.
[NeuronAlias("ai.router"), DefaultNeuronType("ai.router")]
public interface IRouter
{
    // The brain's own router (R4-19: a brain-scoped singleton is keyed main); a script may key its own, {brain}/{name}.
    static string Key(string brainId) => $"{brainId}/main";

    // Record the request and return; the router answers by writing the result into its state and
    // publishing Routed, so a slow model holds no caller.
    Task Ask(RouteRequest request, CancellationToken ct = default);

    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves] Task<PendingRoute?> Pending(Guid id, CancellationToken ct = default);

    // Decide only, for callers that can wait: the catalog contract, method and confidence. No call is made.
    [DigitalBrain.Interleaves] Task<RouteResult> Route(string state, string[]? allowed = null, CancellationToken ct = default);

    // Decide, fill the call's key and arguments with a language model, and invoke the chosen neuron.
    [DigitalBrain.Interleaves] Task<RouteOutcome> Dispatch(string state, string[]? allowed = null, CancellationToken ct = default);

    // Achieve a goal: pick a capability, carry it out, feed the result back, and repeat until the
    // goal is done. This is the router acting as the person's assistant across several actions.
    [DigitalBrain.Interleaves, DigitalBrain.CallTimeout(900000)] Task<GoalRun> Achieve(string goal, string[]? allowed = null, CancellationToken ct = default);
}
