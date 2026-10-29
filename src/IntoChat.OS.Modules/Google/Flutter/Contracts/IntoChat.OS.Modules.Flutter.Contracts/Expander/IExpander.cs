using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Expander;

[NeuronAlias("expander"), DefaultNeuronType(UIVocabulary.ExpanderType)]
public interface IExpander
{
    Task Set(string header, bool expanded, IReadOnlyList<UiChildRef>? children = null);
    Task Toggle();
    [DigitalBrain.ReadOnlyCall] Task<ExpanderState> Read();
}

[ExportableState]
public sealed class ExpanderState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Header { get; set; } = "";
    public bool Expanded { get; set; }
    public List<UiChildRef> Children { get; set; } = [];
}
