using DigitalBrain;
using IntoChat.OS;
namespace IntoChat.OS.Modules.Flutter.Collection;

public sealed record CollectionItem(
    string Id, string Label,
    string Kind, string? SecondaryText = null,
    long? SizeBytes = null, DateTimeOffset? ModifiedAt = null,
    bool CanActivate = true, string? TrailingText = null);

[Signal("ui.collection-activated")]
public sealed record CollectionActivated(string Name, string ItemId, long Revision) : DigitalBrain.Signal;
