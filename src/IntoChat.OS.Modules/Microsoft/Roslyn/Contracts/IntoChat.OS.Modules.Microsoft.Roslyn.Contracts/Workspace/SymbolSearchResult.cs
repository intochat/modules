namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record SymbolSearchResult(
    IReadOnlyList<SymbolHit> Items,
    int TotalCount,
    bool Truncated);
