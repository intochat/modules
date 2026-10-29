using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Button;

[NeuronAlias("button"), DefaultNeuronType(UIVocabulary.ButtonType)]
public interface IButton
{
    Task Set(string label, string action, bool enabled = true);
    Task SetVisual(ControlVisual visual);
    Task Click();
    Task ClickCurrent(string expectedAction);
    Task SetActivation(string label, string activationJson);
    [DigitalBrain.ReadOnlyCall] Task<ButtonState> Read();
}

[ExportableState]
public sealed class ButtonState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Label { get; set; } = "";
    public string Action { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int ClickCount { get; set; }
    public string? Activation { get; set; }
    public ControlVisual? Visual { get; set; }
}

public sealed record ControlVisual(
    string? Icon = null,
    string? Svg = null,
    string? ImageUrl = null,
    bool Compact = false,
    bool Selected = false,
    bool Running = false,
    bool Avatar = false,
    string Tone = "neutral");
