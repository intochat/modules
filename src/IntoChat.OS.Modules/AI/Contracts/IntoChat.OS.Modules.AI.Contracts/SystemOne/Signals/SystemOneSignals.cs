using DigitalBrain;

namespace IntoChat.OS.Modules.AI.SystemOne.Signals;

// One System One answer, as a fact apps and the shell can subscribe to. RequestSummary is the state
// clipped to 2000 characters; Confidence is the highest Choice/Score confidence in Answers (0 if none).
[Signal("ai.systemone-decided")]
public sealed record SystemOneDecided(
    string RequestSummary,
    Dictionary<string, SystemOneAnswer> Answers,
    double Confidence) : DigitalBrain.Signal;
