using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Text;

[NeuronAlias("text"), DefaultNeuronType(UIVocabulary.TextType)]
public interface IText
{
    Task Set(string markdown);
    // Replaces only the version read by the caller, preserving a later writer's text.
    Task<bool> TrySet(string markdown, int expectedVersion);
    [DigitalBrain.ReadOnlyCall] Task<TextState> Read();
}

[ExportableState]
public sealed class TextState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Markdown { get; set; } = "";
}
