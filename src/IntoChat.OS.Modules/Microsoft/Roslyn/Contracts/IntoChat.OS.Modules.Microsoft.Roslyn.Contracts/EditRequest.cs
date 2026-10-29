namespace IntoChat.OS.Modules.Microsoft.Roslyn;

// One edit against the current snapshot. Each kind reads the fields it needs:
// ReplaceMember: SymbolId, Source. InsertMember: SymbolId (a type, or the member to insert after), Source.
// AddUsing: Path, Namespace. ReplaceRange: Path, StartLine, EndLine, Source. Rename: SymbolId, NewName.
// ApplyCodeFix: Path, DiagnosticId, optional StartLine and FixTitle.
public sealed record EditRequest(
    EditKind Kind,
    string? SymbolId = null,
    string? Path = null,
    string? Source = null,
    string? NewName = null,
    int? StartLine = null,
    int? EndLine = null,
    string? Namespace = null,
    string? DiagnosticId = null,
    string? FixTitle = null);
