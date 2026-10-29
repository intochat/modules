using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Collection;

[NeuronAlias("collection"), DefaultNeuronType(UIVocabulary.CollectionType)]
public interface ICollectionView
{
    Task Set(CollectionDefinition definition, long expectedRevision);
    Task Select(string itemId, long expectedRevision);
    Task Activate(string itemId, long expectedRevision);
    [DigitalBrain.ReadOnlyCall] Task<CollectionState> Read();
}

public sealed record CollectionDefinition(CollectionItem[] Items, string? Selection = null, string? Cursor = null, string? Error = null,
    bool FileMode = true, bool SelectionEnabled = true,
    string? EmptyLabel = null, CollectionBreadcrumb[]? Breadcrumbs = null);

public sealed record CollectionBreadcrumb(string Id, string Label);

[ExportableState]
public sealed class CollectionState
{
    public string Name { get; set; } = "";
    public long Revision { get; set; }
    public CollectionDefinition Definition { get; set; } = new([]);
}

[Signal("ui.collection-changed")]
public sealed record CollectionChanged(string Name, long Revision) : DigitalBrain.Signal;
