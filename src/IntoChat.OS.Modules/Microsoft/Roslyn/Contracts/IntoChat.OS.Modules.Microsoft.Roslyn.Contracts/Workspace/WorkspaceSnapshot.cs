namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record WorkspaceSnapshot(
    string? SolutionPath,
    WorkspacePhase Phase,
    int ProjectCount,
    int DocumentCount,
    string? Detail,
    long Generation,
    bool ReloadNeeded = false);
