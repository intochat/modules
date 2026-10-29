namespace IntoChat.OS.Modules.AI.Routing;

// One goal the router worked on, and the ordered actions it took to reach it.
public sealed record GoalRun(
    string Goal,
    GoalStep[] Steps,
    string? Answer = null);

public sealed record GoalStep(
    string Contract,
    string Method,
    string? Key,
    string? Arguments,
    string? Result,
    string? Error,
    double Confidence);
