using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.VoiceInput.Signals;

[Signal("ui.voiceinput-changed")]
public sealed record VoiceInputChanged(string Name, long Revision) : DigitalBrain.Signal;

[Signal("ui.voice-captured")]
public sealed record VoiceCaptured(string Name, byte[] Audio, string MimeType) : DigitalBrain.Signal;
