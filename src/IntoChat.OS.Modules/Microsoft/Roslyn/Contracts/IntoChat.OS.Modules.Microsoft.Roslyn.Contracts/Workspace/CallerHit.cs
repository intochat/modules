namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record CallerHit(
    string Id,
    string Display,
    string Path,
    int Line,
    string Project);
