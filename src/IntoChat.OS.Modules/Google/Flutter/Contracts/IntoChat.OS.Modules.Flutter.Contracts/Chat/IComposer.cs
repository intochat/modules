using DigitalBrain.Contracts;
using DigitalBrain;


namespace IntoChat.OS.Modules.Flutter.Chat;

// Holds input awaiting a behavior's acknowledgement, never conversation history.
[NeuronAlias("composer"), DefaultNeuronType(UIVocabulary.ComposerType)]
public interface IComposer
{
    Task Follow(string chat);
    Task Edit(string context, long selectionRevision, string value);
    Task<Guid?> Submit(Guid requestId, string context, long selectionRevision, string value);
    Task BeginVoice(Guid captureId, string context, long selectionRevision);
    Task Configure(string context, long generation, string label = "Message");
    Task Edit(string context, string value);
    Task<Guid?> Submit(Guid requestId, string context, string value);
    Task Settle(Guid requestId, string? error = null);
    Task Defer(Guid requestId, string error);
    Task BeginVoice(Guid captureId, string context);
    Task CaptureVoice(Guid captureId, byte[] audio, string mimeType);
    Task CancelVoice(Guid captureId);
    Task CompleteVoice(Guid captureId, string? text, string? error = null);
    [DigitalBrain.ReadOnlyCall] Task<ComposerAudio?> ReadAudio(Guid captureId);
    [DigitalBrain.ReadOnlyCall] Task<ComposerState> Read();
}

public sealed record ComposerSubmission(
    Guid Id,
    string Context,
    string Text,
    long DraftVersion,
    string Status = "pending",
    string? Error = null,
    long Generation = -1);

public sealed record ComposerVoice(
    Guid Id,
    string Context,
    long DraftVersion,
    string Status,
    string? Error = null);

public sealed record ComposerAudio(byte[] Bytes, string MimeType);

public sealed record ComposerState
{
    public string Name { get; init; } = "";
    public long Revision { get; init; }
    public string Context { get; init; } = "";
    public long Generation { get; init; } = -1;
    public string Label { get; init; } = "Message";
    public string Draft { get; init; } = "";
    public long DraftVersion { get; init; }
    public ComposerSubmission[] Submissions { get; init; } = [];
    public string? Error { get; init; }
    public ComposerVoice? Voice { get; init; }
    public string? FollowedChat { get; init; }
}

[Signal("ui.composer-changed")]
public sealed record ComposerChanged(string Name, long Revision) : DigitalBrain.Signal;

[Signal("ui.composer-submitted")]
public sealed record ComposerSubmitted(string Name, Guid RequestId) : DigitalBrain.Signal;

[Signal("ui.composer-voice-captured")]
public sealed record ComposerVoiceCaptured(string Name, Guid CaptureId) : DigitalBrain.Signal;
