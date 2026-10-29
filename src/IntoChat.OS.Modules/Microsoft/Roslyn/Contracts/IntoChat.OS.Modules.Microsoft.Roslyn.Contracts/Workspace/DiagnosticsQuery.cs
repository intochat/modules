namespace IntoChat.OS.Modules.Microsoft.Roslyn;

// Exactly one of Path or Project names the scope; both null means the whole solution.
public sealed record DiagnosticsQuery(
    string? Path = null,
    string? Project = null,
    int Limit = 50);
