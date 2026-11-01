namespace IntoChat.OS.Modules.AI.Decision;

public interface IDecisionMaker
{
    /// <summary>
    /// Unified high-level decision method supporting Reactive, Deliberate, and Adaptive modes.
    /// </summary>
    Task<DecisionResult> DecideAsync(DecisionContext context, CancellationToken ct = default);

    /// <summary>
    /// System 1 fast-twitch reactive classification (&lt;10ms via ISystemOne).
    /// </summary>
    Task<FastDecision> DecideReactiveAsync(ReactivePrompt prompt, CancellationToken ct = default);

    /// <summary>
    /// System 2 deliberate cloud reasoning &amp; multi-step goal execution (via IRouter.Achieve).
    /// </summary>
    Task<DeliberateDecision> DecideDeliberateAsync(DeliberateGoal goal, CancellationToken ct = default);
}
