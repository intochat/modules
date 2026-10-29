namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record CallersQuery(string SymbolId, int Limit = 50);
