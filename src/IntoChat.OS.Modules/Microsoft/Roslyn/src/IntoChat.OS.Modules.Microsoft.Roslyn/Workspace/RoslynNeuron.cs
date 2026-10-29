using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Microsoft.Roslyn;

// One workspace per deployment (06-06a): a cluster-scoped singleton in the system brain, keyed by the configured workspace key
// (R4-19), "system/microsoft.roslyn/{WorkspaceKey}".
[Neuron("microsoft.roslyn")]
internal sealed class RoslynNeuron(
    SolutionWorkspace workspace,
    ChangeSetEditor editor,
    TimeProvider clock)
    : DigitalBrain.Neuron<RoslynState>, IRoslyn
{
    internal const string Type = "microsoft.roslyn";

    // The brain cluster-scoped singletons live in (R4-19).
    internal const string SystemBrain = "system";

    private WorkspaceState? Current => State.Workspace;

    private string FullKey => $"{Id.Brain.Value}/{Id.Key}";

    public async Task<WorkspaceReceipt> Open(OpenWorkspace request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.SolutionPath))
        {
            throw new ArgumentException("solution path is blank", nameof(request));
        }

        var generation = Current?.Generation ?? 0;
        if (request.ExpectedVersion is { } expected && expected != generation)
        {
            throw new InvalidOperationException(
                $"expected generation {expected} but the workspace is at {generation}. Read the workspace and retry with the generation it reports.");
        }

        // The load runs in the service; the neuron records the request so a restart reopens it, and reads
        // during the load answer from the durable snapshot and the last good map.
        await workspace.BeginOpenAsync(request.SolutionPath);
        var map = await CacheMapAsync();
        generation++;
        await Save(new RoslynState { Workspace = new WorkspaceState(request.SolutionPath, generation, clock.GetUtcNow(), map, workspace.Status.Detail) },
            new WorkspaceChanged(FullKey, generation, workspace.Status.Phase));
        return new WorkspaceReceipt(FullKey, generation);
    }

    public async Task<WorkspaceReceipt> Reload()
    {
        var path = Current?.SolutionPath ?? workspace.Status.SolutionPath;
        if (path is null)
        {
            throw new InvalidOperationException("No solution has been opened. Open a solution before reloading.");
        }

        if (workspace.Status.Phase == WorkspacePhase.NotOpened)
        {
            await workspace.BeginOpenAsync(path);
        }
        else
        {
            await workspace.BeginReloadAsync();
        }

        var map = await CacheMapAsync();
        var generation = (Current?.Generation ?? 0) + 1;
        await Save(new RoslynState { Workspace = new WorkspaceState(path, generation, clock.GetUtcNow(), map, workspace.Status.Detail) },
            new WorkspaceChanged(FullKey, generation, workspace.Status.Phase));
        return new WorkspaceReceipt(FullKey, generation);
    }

    [DigitalBrain.ReadOnlyCall]
    public Task<WorkspaceSnapshot> Read()
    {
        var live = workspace.Status;
        return Task.FromResult(new WorkspaceSnapshot(
            Current?.SolutionPath ?? live.SolutionPath,
            live.Phase,
            live.ProjectCount,
            live.DocumentCount,
            live.Detail ?? Current?.Detail,
            Current?.Generation ?? 0,
            live.ReloadNeeded));
    }

    [DigitalBrain.ReadOnlyCall]
    public Task<SymbolSearchResult> FindSymbols(SymbolSearch query, CancellationToken cancellationToken = default)
        => workspace.FindSymbolsAsync(query, cancellationToken);

    [DigitalBrain.ReadOnlyCall]
    public Task<ReferenceSearchResult> References(ReferenceSearch query, CancellationToken cancellationToken = default)
        => workspace.ReferencesAsync(query, cancellationToken);

    [DigitalBrain.ReadOnlyCall]
    public Task<DiagnosticsResult> Diagnostics(DiagnosticsQuery query, CancellationToken cancellationToken = default)
        => workspace.DiagnosticsAsync(query, cancellationToken);

    [DigitalBrain.ReadOnlyCall]
    public async Task<SolutionMap> Map(MapQuery query, CancellationToken cancellationToken = default)
    {
        if (workspace.Status.Phase == WorkspacePhase.Ready)
        {
            return await workspace.MapAsync(query, cancellationToken);
        }

        return Current?.LastMap ?? throw new WorkspaceNotReadyException(workspace.Status);
    }

    [DigitalBrain.ReadOnlyCall]
    public Task<Skeleton> Skeleton(SkeletonQuery query, CancellationToken cancellationToken = default)
        => workspace.SkeletonAsync(query, cancellationToken);

    [DigitalBrain.ReadOnlyCall]
    public Task<MemberSource> Member(MemberQuery query, CancellationToken cancellationToken = default)
        => workspace.MemberAsync(query, cancellationToken);

    [DigitalBrain.ReadOnlyCall]
    public Task<CallersResult> Callers(CallersQuery query, CancellationToken cancellationToken = default)
        => workspace.CallersAsync(query, cancellationToken);

    [DigitalBrain.ReadOnlyCall]
    public Task<SymbolSearchResult> Implementations(ImplementationsQuery query, CancellationToken cancellationToken = default)
        => workspace.ImplementationsAsync(query, cancellationToken);

    [DigitalBrain.ReadOnlyCall]
    public Task<SymbolSearchResult> Derived(DerivedQuery query, CancellationToken cancellationToken = default)
        => workspace.DerivedAsync(query, cancellationToken);

    public async Task<EditCheck> CheckEdits(IReadOnlyList<EditRequest> edits, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var outcome = await workspace.QueryAsync((solution, token) => editor.ApplyAsync(solution, edits, token), cancellationToken);
        return new EditCheck(outcome.Diagnostics, outcome.Diff, outcome.FailingEdit, outcome.Detail, outcome.HasErrors);
    }

    public async Task<EditCommit> CommitEdits(IReadOnlyList<EditRequest> edits, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(edits);
        EditOutcome? applied = null;
        try
        {
            var committed = await workspace.CommitAsync(async (solution, token) =>
            {
                applied = await editor.ApplyAsync(solution, edits, token);
                if (applied.HasErrors)
                {
                    throw new InvalidOperationException(applied.Detail ?? "the change set has errors");
                }

                return applied.Changed;
            }, cancellationToken);
            return new EditCommit(applied!.Diagnostics, applied.Diff, committed.WrittenPaths, committed.SnapshotVersion, null, false);
        }
        catch (InvalidOperationException error) when (applied is { HasErrors: true })
        {
            return new EditCommit(applied.Diagnostics, applied.Diff, [], 0, error.Message, true);
        }
    }

    // A silo that restarted still knows which solution this workspace had open.
    protected override Task OnActivate(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Current is { } current && workspace.Status.Phase == WorkspacePhase.NotOpened)
        {
            _ = workspace.BeginOpenAsync(current.SolutionPath);
        }

        return Task.CompletedTask;
    }

    private async Task<SolutionMap?> CacheMapAsync()
        => workspace.Status.Phase == WorkspacePhase.Ready
            ? await workspace.MapAsync(new MapQuery(), CancellationToken.None)
            : Current?.LastMap;
}

// The workspace neuron's state: the open request, durable across restarts (06-06a; WorkspaceState was the grain's state).
[GenerateSerializer, Alias("coding.roslyn-state")]
[DigitalBrain.State.ExportableState]
internal sealed class RoslynState
{
    [Id(0)] public WorkspaceState? Workspace { get; init; }
}
