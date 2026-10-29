namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record DiagnosticsResult(
    IReadOnlyList<DiagnosticHit> Items,
    int ErrorCount,
    int WarningCount,
    bool Truncated,
    int TotalCount);
