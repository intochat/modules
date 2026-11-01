namespace IntoChat.OS.Modules.AI.Decision;

public sealed record DecisionContext(
    string Intent,
    IReadOnlyList<string> Candidates,
    IReadOnlyDictionary<string, string>? Metadata = null,
    DecisionMode Mode = DecisionMode.Adaptive,
    double ConfidenceThreshold = 0.5,
    int MaxSteps = 8);
