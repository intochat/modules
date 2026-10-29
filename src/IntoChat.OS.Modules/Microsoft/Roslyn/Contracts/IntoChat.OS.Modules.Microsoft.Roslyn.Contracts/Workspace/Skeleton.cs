namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record Skeleton(
    string Path,
    string Project,
    IReadOnlyList<SkeletonMember> Members);
