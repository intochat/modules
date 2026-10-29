using DigitalBrain;

namespace IntoChat.OS.Modules.AI.Agents.Signals;

[Signal("agent.configured")]
public sealed record AgentConfigured(string AgentId, long Revision) : DigitalBrain.Signal;

[Signal("agent.turn-started")]
public sealed record TurnStarted(string AgentId, string RunId, string UserText) : DigitalBrain.Signal
{
    // Persisted conversation version at publication; zero means no version was supplied.
    public long Version { get; init; }
}

// Sequence numbers a run's progress from 1; delivery is at-least-once, so handlers key their
// writes by (RunId, Sequence). Kind is text, tool-call, tool-result, card or receipt. A text step
// holds the text streamed since the previous one (chunks are coalesced); in sequence they are the reply.
[Signal("agent.turn-progressed")]
public sealed record TurnProgressed(string AgentId, string RunId, string Kind, string? Text, string? Tool, string? Payload, int Sequence) : DigitalBrain.Signal
{
    public long Version { get; init; }
}

[Signal("agent.turn-completed")]
public sealed record TurnCompleted(string AgentId, string RunId, string Reply) : DigitalBrain.Signal
{
    public long Version { get; init; }
}

[Signal("agent.turn-failed")]
public sealed record TurnFailed(string AgentId, string RunId, string Error, bool Cancelled) : DigitalBrain.Signal
{
    public long Version { get; init; }
}

[Signal("agent.transcript-cleared")]
public sealed record TranscriptCleared(string AgentId, long Revision) : DigitalBrain.Signal;

public static class AgentSignals
{
    // The agent neuron's type.
    public const string NeuronType = "agent";

    // Every signal an agent publishes: what a follower (a chat) holds its synapse on.
    public static Type[] All { get; } =
        [typeof(AgentConfigured), typeof(TurnStarted), typeof(TurnProgressed), typeof(TurnCompleted), typeof(TurnFailed), typeof(TranscriptCleared)];
}
