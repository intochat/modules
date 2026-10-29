namespace IntoChat.OS.Modules.Flutter.Workspace;

public sealed record WorkspaceState(
    long Revision,
    string[] Path,
    string[] Open)
{
    public WorkspaceViewRef[] OpenViews { get; init; } = [];
    public string? FocusedViewId { get; init; }
    public WorkspaceViewRef[] Pins { get; init; } = [];
    public WorkspaceWindow[] Windows { get; init; } = [];
    public WorkspaceViewRef[] Favourites { get; init; } = [];
}
