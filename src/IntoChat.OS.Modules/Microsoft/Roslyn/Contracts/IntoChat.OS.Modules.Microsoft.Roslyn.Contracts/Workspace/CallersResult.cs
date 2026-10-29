namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record CallersResult(
    string SymbolId,
    IReadOnlyList<CallerHit> Items,
    int TotalCount,
    bool Truncated);
