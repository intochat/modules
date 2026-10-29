namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record SymbolHit(
    string Id,
    string Kind,
    string Name,
    string Display,
    string Project,
    string Path,
    int Line);
