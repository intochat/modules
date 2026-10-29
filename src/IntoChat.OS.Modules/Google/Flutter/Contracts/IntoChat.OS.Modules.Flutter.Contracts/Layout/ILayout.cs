using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Layout;

[NeuronAlias("layout"), DefaultNeuronType(UIVocabulary.LayoutType)]
public interface ILayout
{
    Task Set(LayoutDefinition definition, long expectedRevision);
    [DigitalBrain.ReadOnlyCall] Task<LayoutState> Read();
}

public sealed record LayoutDefinition(string Mode, UiChildRef[] Children, double Gap = 12, double[]? Extents = null,
    bool FollowEnd = false,
    string? Surface = null, double Padding = 0);

[ExportableState]
public sealed class LayoutState
{
    public string Name { get; set; } = "";
    public long Revision { get; set; }
    public LayoutDefinition Definition { get; set; } = new("column", []);
}

[Signal("ui.layout-changed")]
public sealed record LayoutChanged(string Name, long Revision) : DigitalBrain.Signal;
