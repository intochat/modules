namespace IntoChat.OS.Modules.AI.Decision;

public sealed record DeliberateGoal(
    string Objective,
    string[]? AvailableCapabilities = null,
    int MaxSteps = 8);
