using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter.Button;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Select;

[NeuronAlias("select"), DefaultNeuronType(UIVocabulary.SelectType)]
public interface ISelect
{
    Task Set(string label, IReadOnlyList<SelectOption> options, string? selected);
    Task SetVisual(ControlVisual visual);
    Task Choose(string value);
    [DigitalBrain.ReadOnlyCall] Task<SelectState> Read();
}

public sealed record SelectOption(string Id, string Label, bool Enabled = true, ControlVisual? Visual = null);

[ExportableState]
public sealed class SelectState
{
    public string Name { get; set; } = "";
    public long Revision { get; set; }
    public string Label { get; set; } = "";
    public SelectOption[] Options { get; set; } = [];
    public string? Selected { get; set; }
    public ControlVisual? Visual { get; set; }
}

[Signal("ui.select-changed")]
public sealed record SelectChanged(string Name, string Value) : DigitalBrain.Signal;
