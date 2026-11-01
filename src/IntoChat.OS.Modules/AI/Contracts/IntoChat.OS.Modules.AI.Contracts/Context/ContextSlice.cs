namespace IntoChat.OS.Modules.AI.Context;

/// <summary>
/// A named, tagged context slice for prompt assembly and execution scoping.
/// </summary>
/// <param name="Name">Distinct identifier for the slice (e.g. "spec", "manifest", "active_file").</param>
/// <param name="Content">The textual payload.</param>
/// <param name="Tags">Metadata tags identifying the slice role or contracts.</param>
public sealed record ContextSlice(
    string Name,
    string Content,
    string[] Tags)
{
    public static ContextSlice Create(string name, string content, params string[] tags)
        => new(name, content, tags);
}
