using IntoChat.OS.Modules.AI.Routing;

namespace IntoChat.OS.Modules.AI.Decision;

public sealed record DecisionResult(
    string Choice,
    double Confidence,
    DecisionMode ModeUsed,
    IReadOnlyDictionary<string, double>? Probabilities = null,
    string? Plan = null,
    GoalRun? Outcome = null);
