using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IntoChat.OS.Modules.Postgres.Tables.Signals;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Postgres.Tables;

[GenerateSerializer, Alias("postgres.table-state")]
[ExportableState]
internal sealed record TableState
{
    [Id(0)] public string? Owner { get; init; }
    [Id(1)] public AcceptedTable? Accepted { get; init; }
    [Id(2)] public TablePlacement? Placement { get; init; }
    [Id(3)] public TableDefinition? Pending { get; init; }
    [Id(4)] public string? PhysicalTable { get; init; }
}

// A table's own state changes that carry no row signal (D-02: a plain neuron saves with the signal that says what changed):
// the placement pinned before the physical table exists, and the table retired with its app's storage.
[DigitalBrain.Signal("postgres.table.pending")]
internal sealed record TablePending(string Table) : DigitalBrain.Signal;

[DigitalBrain.Signal("postgres.table.retired")]
internal sealed record TableRetired(string Table) : DigitalBrain.Signal;

[DigitalBrain.Neuron(GrainType)]
internal sealed partial class TableNeuron(ITableStore provider, ITablePlacements placements)
    : DigitalBrain.Neuron<TableState>, ITable, ITableLifetime
{
    public const string GrainType = "postgres.table";

    // The os key "{brain}/{key}" a table is named by in its index and its physical name.
    private string FullKey => $"{Id.Brain.Value}/{Id.Key}";

    // Only the owning brain and app may hold a synapse on its rows (R4-03: the rule the Watch override kept). The caller is the
    // trusted stamp the runtime read from the subscriber's call (R4-20), never anything the subscriber sends: no stamp, a stamp
    // without an app, or a stamp of another brain or app is refused.
    protected override Task AdmitSynapse(DigitalBrain.SynapseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Subscriber.Brain != Id.Brain) { throw new UnauthorizedAccessException("This table belongs to another brain or app."); }
        if (request.Caller is not { AppId: { Length: > 0 } app } caller || !BrainScope.IsValidId(caller.BrainId))
        { throw new UnauthorizedAccessException("A trusted brain and app caller is required."); }
        if (State.Owner is { } owner && owner != new TableOwner(caller.BrainId, app).Key)
        { throw new UnauthorizedAccessException("This table belongs to another brain or app."); }
        return Task.CompletedTask;
    }

    private string Scope()
    {
        var scope = TableOwner.Current().Key;
        if (State.Owner is { } owner && owner != scope)
        { throw new UnauthorizedAccessException("This table belongs to another brain or app."); }
        return scope;
    }

    private async Task<AcceptedTable> Current()
    {
        // Check each operation: caching would allow writes after the owner closes but before
        // its queued Retire reaches this table. Table serialization then orders accepted work before DROP.
        await Tables(Scope()).RequireOpen();
        return State.Accepted ?? throw new PostgresQueryException("Define the table first.");
    }

    // Retire can reach a table whose Define never persisted; there is nothing placed elsewhere to reclaim.
    private TablePlacement Pinned => State.Placement ?? TablePlacement.Shared;

    private ITableStore Tables(TablePlacement placement)
        => placement.Kind == TablePlacementKind.Connection ? new HostedTables(Context.Get<IPostgresTableHost>(IntoChat.OS.Connections.Platform.Connections.ConnectionGrains.IdFor(placement.Name!))) : provider;

    // The index is a plain neuron (06-08) reached as this neuron, so TableLifecycleGuard sees this table's host as the caller.
    private ITableIndex Tables(string owner)
    {
        var brain = BrainKey.ScopeOf(owner) ?? throw new UnauthorizedAccessException("This storage key does not name a brain.");
        return Context.Get<ITableIndex>(new DigitalBrain.NeuronId(new DigitalBrain.BrainId(brain), TableIndexNeuron.GrainType, owner[(brain.Length + 1)..]));
    }

    public async Task Retire(string owner)
    {
        if (State.Owner != owner) { return; }
        var table = State.Accepted?.Table ?? State.PhysicalTable ?? TablePolicy.PhysicalName(owner, FullKey);
        try { await Tables(Pinned).DropAsync(Pinned, table, CancellationToken.None); }
        // A revoked or deleted connection holds the user's own database: nothing here can reach it,
        // and an uninstall must still retire the app's other tables.
        catch (Exception error) when (Pinned.Kind == TablePlacementKind.Connection && error is ServiceUnavailableException or UnauthorizedAccessException)
        { Context.Services.GetRequiredService<ILogger<TableNeuron>>().LogSwallowed(error, "dropping a table on an unreachable connection while uninstalling"); }
        await Save(new(), new TableRetired(table));
    }

    public async Task<AcceptedTable> Define(TableDefinition definition)
    {
        var scope = Scope();
        var normalized = TablePolicy.Validate(definition);
        await Tables(scope).RequireOpen();
        if (State.Accepted is { } accepted)
        {
            if (!TablePolicy.Compatible(accepted.Definition, normalized))
            { throw new PostgresQueryException("Incompatible table redefinition; migrations are not supported."); }
            await Tables(scope).Register(FullKey);
            return accepted;
        }
        var table = State.PhysicalTable ?? TablePolicy.PhysicalName(scope, FullKey);
        // The placement is pinned at first Define; a later policy or binding never moves a table.
        if (State.Pending is { } pending && !TablePolicy.Compatible(pending, normalized))
        { throw new PostgresQueryException("Incompatible table redefinition; migrations are not supported."); }
        var placement = State.Placement ?? await placements.Resolve(TableOwner.Current(), CancellationToken.None);
        await Tables(scope).Register(FullKey);
        await Save(State with { Owner = scope, Placement = placement, Pending = normalized }, new TablePending(table));
        await Tables(placement).DefineAsync(placement, table, normalized, CancellationToken.None);
        var result = new AcceptedTable(table, normalized, 1);
        await Save(new() { Owner = scope, Accepted = result, Placement = placement }, new TableDefined(table, result.Revision));
        return result;
    }

    public async Task<bool> Upsert(TableValue[] key, TableValue[] values)
    {
        var current = await Current();
        key = TablePolicy.Values(current.Definition, key, true);
        values = TablePolicy.Values(current.Definition, values, false);
        var changed = await Tables(Pinned).UpsertAsync(Pinned, current.Table, current.Definition, key, values, CancellationToken.None);
        if (changed) { await Publish(new RowUpserted(current.Table, key)); }
        return changed;
    }

    public async Task<bool> Delete(TableValue[] key)
    {
        var current = await Current();
        key = TablePolicy.Values(current.Definition, key, true);
        var changed = await Tables(Pinned).DeleteAsync(Pinned, current.Table, current.Definition, key, CancellationToken.None);
        if (changed) { await Publish(new RowDeleted(current.Table, key)); }
        return changed;
    }

    public async Task<TableValue[]?> Read(TableValue[] key)
    {
        var current = await Current();
        key = TablePolicy.Values(current.Definition, key, true);
        return await Tables(Pinned).ReadAsync(Pinned, current.Table, current.Definition, key, CancellationToken.None);
    }

    public async Task<TableValue[][]> Page(int offset = 0, int limit = 200)
    {
        var current = await Current();
        if (offset < 0 || limit is < 1 or > PostgresQuery.MaxRowsLimit)
        { throw new PostgresQueryException($"Offset must be nonnegative and limit must be 1–{PostgresQuery.MaxRowsLimit}."); }
        return await Tables(Pinned).PageAsync(Pinned, current.Table, current.Definition, offset, limit, CancellationToken.None);
    }
}

// A Connection placement's store: every operation runs inside the connection's neuron.
internal sealed class HostedTables(IPostgresTableHost host) : ITableStore
{
    public Task DefineAsync(TablePlacement placement, string table, TableDefinition definition, CancellationToken ct) => host.Define(table, definition);
    public Task DropAsync(TablePlacement placement, string table, CancellationToken ct) => host.Drop(table);
    public Task<bool> UpsertAsync(TablePlacement placement, string table, TableDefinition definition, TableValue[] key, TableValue[] values, CancellationToken ct) => host.Upsert(table, definition, key, values);
    public Task<bool> DeleteAsync(TablePlacement placement, string table, TableDefinition definition, TableValue[] key, CancellationToken ct) => host.Delete(table, definition, key);
    public Task<TableValue[]?> ReadAsync(TablePlacement placement, string table, TableDefinition definition, TableValue[] key, CancellationToken ct) => host.Read(table, definition, key);
    public Task<TableValue[][]> PageAsync(TablePlacement placement, string table, TableDefinition definition, int offset, int limit, CancellationToken ct) => host.Page(table, definition, offset, limit);
}
