using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.TextField;

[NeuronAlias("textfield"), DefaultNeuronType(UIVocabulary.TextFieldType)]
public interface ITextField
{
    Task Configure(string label, string kind, string? submitButton = null);
    Task SetValue(string value);
    Task<bool> TrySetValue(string value, int expectedVersion);
    Task Input(string value);
    [DigitalBrain.ReadOnlyCall] Task<TextFieldState> Read();
}

[ExportableState]
public sealed class TextFieldState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Label { get; set; } = "";
    public string Kind { get; set; } = "text";
    public string Value { get; set; } = "";
    public string? SubmitButton { get; set; }
}
