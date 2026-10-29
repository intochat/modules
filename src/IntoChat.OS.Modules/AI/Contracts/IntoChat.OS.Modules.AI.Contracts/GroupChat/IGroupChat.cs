using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.AI.GroupChat;

// Several models discuss one question in turns, building on each other, until they agree or run out of rounds.
[NeuronAlias("ai.group-chat"), DefaultNeuronType("ai.group-chat")]
public interface IGroupChat
{
    Task<GroupChatState> Configure(GroupChatSetup setup);
    [DigitalBrain.CallTimeout(1800000)] Task<GroupChatState> Ask(string question);
    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves] Task<GroupChatState> Read();
}

// Model is the LLM marker name, for example "IGpt56Luna" or "IGemma4".
public sealed record GroupChatParticipant(
    string Name,
    string Model,
    string Instructions = "");

// The first participant also moderates: it writes the final answer.
public sealed record GroupChatSetup(
    GroupChatParticipant[] Participants,
    string Brief = "",
    int MaxRounds = 3);

public sealed record GroupChatTurn(
    int Round,
    string Speaker,
    string Text);

public enum GroupChatStatus { Idle, Discussing, Concluded, Failed }

[ExportableState]
public sealed record GroupChatState
{
    public long Revision { get; init; }
    public GroupChatSetup? Setup { get; init; }
    public string Question { get; init; } = "";
    public GroupChatTurn[] Turns { get; init; } = [];
    public string Answer { get; init; } = "";
    public GroupChatStatus Status { get; init; }
    public bool Agreed { get; init; }
    public string Failure { get; init; } = "";
}
