using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Surface;

[Neuron(UIVocabulary.SurfaceType)]
internal sealed class SurfaceNeuron
    : UiNeuron<SurfaceState>, ISurface
{
    public async Task Set(SurfaceDefinition definition, long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Title);
        CompositionValidation.Children(definition.Children);
        if (State.Revision != expectedRevision) { throw new RevisionConflictException("The UI changed. Read its latest revision before retrying.", State.Revision); }
        // The state and its signal are one journal entry (D-02).
        var next = new SurfaceState { Name = FullKey, Revision = checked(State.Revision + 1), Definition = definition };
        await Save(next, new SurfaceChanged(FullKey, next.Revision));
    }

    public Task<SurfaceState> Read() => Task.FromResult(State);
}
