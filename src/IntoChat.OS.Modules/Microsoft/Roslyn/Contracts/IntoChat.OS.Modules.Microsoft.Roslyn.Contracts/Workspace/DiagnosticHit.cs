namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record DiagnosticHit(
    string Id,
    string Severity,
    string Message,
    string Path,
    int Line);
