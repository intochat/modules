namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record OpenWorkspace(
    string SolutionPath,
    long? ExpectedVersion = null);
