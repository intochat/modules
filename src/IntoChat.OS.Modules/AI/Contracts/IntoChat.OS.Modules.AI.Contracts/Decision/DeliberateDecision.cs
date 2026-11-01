using IntoChat.OS.Modules.AI.Routing;

namespace IntoChat.OS.Modules.AI.Decision;

public sealed record DeliberateDecision(
    string Plan,
    GoalRun Outcome);
