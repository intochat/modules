using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.VoiceInput;

// Captures audio from the user; whoever listens decides how to transcribe it.
[NeuronAlias("voiceinput"), DefaultNeuronType(UIVocabulary.VoiceInputType)]
public interface IVoiceInput
{
    const int MaxAudioBytes = 8 * 1024 * 1024;

    Task Configure(string label);
    Task Capture(byte[] audio, string mimeType);
    [DigitalBrain.ReadOnlyCall] Task<VoiceInputState> Read();
}

[ExportableState]
public sealed class VoiceInputState
{
    public string Name { get; set; } = "";
    public long Revision { get; set; }
    public string Label { get; set; } = "";
    public int Captures { get; set; }
}
