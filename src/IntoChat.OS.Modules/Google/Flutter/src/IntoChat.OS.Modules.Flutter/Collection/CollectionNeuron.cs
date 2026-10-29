using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Collection;

[Neuron(UIVocabulary.CollectionType)]
internal sealed class CollectionNeuron
    : UiNeuron<CollectionState>, ICollectionView
{
    public async Task Set(CollectionDefinition definition, long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Items.Length > 500 || definition.Items.Select(x => x.Id).Distinct().Count() != definition.Items.Length) { throw new ArgumentException("Collection items must have unique IDs and contain at most 500 items."); }
        if (State.Revision != expectedRevision) { throw new RevisionConflictException("The UI changed. Read its latest revision before retrying.", State.Revision); }
        // The state and its signal are one journal entry (D-02).
        var next = new CollectionState { Name = FullKey, Revision = checked(State.Revision + 1), Definition = definition };
        await Save(next, new CollectionChanged(FullKey, next.Revision));
    }

    public async Task Select(string itemId, long expectedRevision)
    {
        if (!State.Definition.Items.Any(x => x.Id == itemId)) { throw new ArgumentException("Unknown collection item."); }
        await Set(State.Definition with { Selection = itemId }, expectedRevision);
    }
    public async Task Activate(string itemId, long expectedRevision)
    {
        if (State.Revision != expectedRevision) { throw new RevisionConflictException("The collection changed. Refresh it first.", State.Revision); }
        if (!State.Definition.Items.Any(x => x.Id == itemId && x.CanActivate) &&
            !(State.Definition.Breadcrumbs?.Any(x => x.Id == itemId) ?? false)) { throw new ArgumentException("This item cannot be opened."); }
        await Publish(new CollectionActivated(FullKey, itemId, expectedRevision));
    }
    public Task<CollectionState> Read() => Task.FromResult(State);
}
