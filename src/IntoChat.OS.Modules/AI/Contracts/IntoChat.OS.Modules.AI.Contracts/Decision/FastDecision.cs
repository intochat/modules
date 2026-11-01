namespace IntoChat.OS.Modules.AI.Decision;

public sealed record FastDecision(
    string Choice,
    double Confidence,
    IReadOnlyDictionary<string, double> Probabilities);
