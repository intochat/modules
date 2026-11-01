namespace IntoChat.OS.Modules.AI.Decision;

public enum DecisionMode
{
    Reactive,   // System 1 fast-twitch reactive classification (<10ms via ISystemOne)
    Deliberate, // System 2 iterative cloud reasoning & goal execution (via IRouter.Achieve)
    Adaptive    // Attempt Reactive first; if confidence < ConfidenceThreshold, escalate to Deliberate
}
