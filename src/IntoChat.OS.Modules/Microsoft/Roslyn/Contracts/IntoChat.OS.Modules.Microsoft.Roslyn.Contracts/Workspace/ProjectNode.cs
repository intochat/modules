namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record ProjectNode(
    string Name,
    string Path,
    string Cluster,
    int DocumentCount);
