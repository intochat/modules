using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Layout;

[Neuron(UIVocabulary.LayoutType)]
internal sealed class LayoutNeuron
    : UiNeuron<LayoutState>, ILayout
{
    public async Task Set(LayoutDefinition definition, long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Mode is not ("row" or "column" or "split" or "stack" or "list")) { throw new ArgumentException("Unknown layout mode."); }
        if (!double.IsFinite(definition.Gap) || definition.Gap < 0 || definition.Gap > 128) { throw new ArgumentException("Gap must be between 0 and 128."); }
        if (definition.Extents is { } extents && (extents.Length != definition.Children.Length || extents.Any(x => !double.IsFinite(x) || x < 0 || x > 4096))) { throw new ArgumentException("Extents must match children and be between 0 and 4096."); }
        if (definition.Surface is not (null or "panel" or "floating")) { throw new ArgumentException("Unknown surface material."); }
        if (!double.IsFinite(definition.Padding) || definition.Padding < 0 || definition.Padding > 128) { throw new ArgumentException("Padding must be between 0 and 128."); }
        CompositionValidation.Children(definition.Children);
        if (State.Revision != expectedRevision) { throw new RevisionConflictException("The UI changed. Read its latest revision before retrying.", State.Revision); }
        // The state and its signal are one journal entry (D-02).
        var next = new LayoutState { Name = FullKey, Revision = checked(State.Revision + 1), Definition = definition };
        await Save(next, new LayoutChanged(FullKey, next.Revision));
    }

    public Task<LayoutState> Read() => Task.FromResult(State);
}
