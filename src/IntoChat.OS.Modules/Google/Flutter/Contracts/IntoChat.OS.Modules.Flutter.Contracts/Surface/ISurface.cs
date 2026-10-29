using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Surface;

[NeuronAlias("surface"), DefaultNeuronType(UIVocabulary.SurfaceType)]
public interface ISurface
{
    Task Set(SurfaceDefinition definition, long expectedRevision);
    [DigitalBrain.ReadOnlyCall] Task<SurfaceState> Read();
}

public sealed record SurfaceDefinition(string Title, UiChildRef[] Children);

[ExportableState]
public sealed class SurfaceState
{
    public string Name { get; set; } = "";
    public long Revision { get; set; }
    public SurfaceDefinition Definition { get; set; } = new("", []);
}

[Signal("ui.surface-changed")]
public sealed record SurfaceChanged(string Name, long Revision) : DigitalBrain.Signal;
