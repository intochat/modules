namespace IntoChat.OS.Modules.Postgres.Tables;

public enum TablePlacementKind { Shared, BrainDatabase, Connection }

// Where a table lives, pinned at first Define. Name is the database (BrainDatabase) or the
// connection grain key (Connection); Shared has none. Flat on purpose: JSON grain storage.
[GenerateSerializer, Alias("postgres.table-placement")]
public sealed record TablePlacement([property: Id(0)] TablePlacementKind Kind, [property: Id(1)] string? Name = null)
{
    public static readonly TablePlacement Shared = new(TablePlacementKind.Shared);
    public static TablePlacement BrainDatabase(string name) => new(TablePlacementKind.BrainDatabase, name);
    public static TablePlacement Connection(string key) => new(TablePlacementKind.Connection, key);
}
