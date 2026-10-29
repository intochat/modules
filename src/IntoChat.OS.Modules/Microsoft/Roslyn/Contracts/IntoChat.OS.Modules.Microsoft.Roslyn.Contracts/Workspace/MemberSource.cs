namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record MemberSource(
    string Id,
    string Path,
    int StartLine,
    int EndLine,
    string Source);
