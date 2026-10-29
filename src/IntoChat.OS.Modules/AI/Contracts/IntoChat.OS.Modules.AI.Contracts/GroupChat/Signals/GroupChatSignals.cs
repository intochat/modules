using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.AI.GroupChat.Signals;

[Signal("ai.group-chat-changed")]
public sealed record GroupChatChanged(string ChatId, long Revision, GroupChatStatus Status) : DigitalBrain.Signal;

[Signal("ai.group-chat-spoke")]
public sealed record GroupChatSpoke(string ChatId, GroupChatTurn Turn) : DigitalBrain.Signal;

[Signal("ai.group-chat-concluded")]
public sealed record GroupChatConcluded(
    string ChatId,
    string Answer,
    int Rounds,
    bool Agreed) : DigitalBrain.Signal;
