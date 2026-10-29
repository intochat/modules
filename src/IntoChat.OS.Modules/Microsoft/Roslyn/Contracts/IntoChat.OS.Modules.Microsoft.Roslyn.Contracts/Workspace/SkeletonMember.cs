namespace IntoChat.OS.Modules.Microsoft.Roslyn;

public sealed record SkeletonMember(
    string Id,
    string Kind,
    string Signature,
    int Line,
    int Depth);
