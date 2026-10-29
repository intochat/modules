using DigitalBrain.State;

namespace IntoChat.OS.Modules.Microsoft.Roslyn;

[GenerateSerializer]
[Alias("coding.workspace-state")]
[ExportableState]
internal sealed record WorkspaceState(
    [property: Id(0)] string SolutionPath,
    [property: Id(1)] long Generation,
    [property: Id(2)] DateTimeOffset RequestedAt,
    [property: Id(3)] SolutionMap? LastMap = null,
    [property: Id(4)] string? Detail = null);
