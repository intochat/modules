namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record ReferenceSearchResult(
    string SymbolId,
    IReadOnlyList<ReferenceHit> Items,
    int TotalCount,
    bool Truncated);
