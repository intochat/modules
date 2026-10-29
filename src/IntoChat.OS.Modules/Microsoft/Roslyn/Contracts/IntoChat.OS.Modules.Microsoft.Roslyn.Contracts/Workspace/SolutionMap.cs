namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record SolutionMap(
    string SolutionPath,
    ProjectNode[] Projects,
    ProjectEdge[] References);
