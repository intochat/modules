using DigitalBrain.Contracts;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Microsoft.Roslyn;

[NeuronAlias("microsoft.roslyn"), DefaultNeuronType("microsoft.roslyn")]
public interface IRoslyn
{
    Task<WorkspaceReceipt> Open(OpenWorkspace request);

    Task<WorkspaceReceipt> Reload();

    [DigitalBrain.ReadOnlyCall]
    Task<WorkspaceSnapshot> Read();

    [DigitalBrain.ReadOnlyCall]
    Task<SymbolSearchResult> FindSymbols(SymbolSearch query, CancellationToken cancellationToken = default);

    [DigitalBrain.ReadOnlyCall]
    Task<ReferenceSearchResult> References(ReferenceSearch query, CancellationToken cancellationToken = default);

    [DigitalBrain.ReadOnlyCall]
    Task<DiagnosticsResult> Diagnostics(DiagnosticsQuery query, CancellationToken cancellationToken = default);

    [DigitalBrain.ReadOnlyCall]
    Task<SolutionMap> Map(MapQuery query, CancellationToken cancellationToken = default);

    [DigitalBrain.ReadOnlyCall]
    Task<Skeleton> Skeleton(SkeletonQuery query, CancellationToken cancellationToken = default);

    [DigitalBrain.ReadOnlyCall]
    Task<MemberSource> Member(MemberQuery query, CancellationToken cancellationToken = default);

    [DigitalBrain.ReadOnlyCall]
    Task<CallersResult> Callers(CallersQuery query, CancellationToken cancellationToken = default);

    [DigitalBrain.ReadOnlyCall]
    Task<SymbolSearchResult> Implementations(ImplementationsQuery query, CancellationToken cancellationToken = default);

    [DigitalBrain.ReadOnlyCall]
    Task<SymbolSearchResult> Derived(DerivedQuery query, CancellationToken cancellationToken = default);

    Task<EditCheck> CheckEdits(IReadOnlyList<EditRequest> edits, CancellationToken cancellationToken = default);

    Task<EditCommit> CommitEdits(IReadOnlyList<EditRequest> edits, CancellationToken cancellationToken = default);
}

public sealed record EditCheck(
    IReadOnlyList<DiagnosticHit> Diagnostics,
    string Diff,
    int? FailingEdit,
    string? Detail,
    bool HasErrors);

public sealed record EditCommit(
    IReadOnlyList<DiagnosticHit> Diagnostics,
    string Diff,
    IReadOnlyList<string> WrittenPaths,
    long SnapshotVersion,
    string? Detail,
    bool HasErrors);
