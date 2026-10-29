namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record ReferenceHit(
    string Path,
    int Line,
    string Project,
    string Text,
    bool Generated);
