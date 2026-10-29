using System.Text.Json.Serialization;

namespace IntoChat.OS.Modules.Flutter.Workspace;

public sealed record WorkspaceViewRef(string Id, string App,
    string Variant, string Context);

public sealed record WorkspaceUpdate(WorkspaceViewRef[] OpenViews,
    string? FocusedViewId, WorkspaceViewRef[] Pins)
{
    // Null keeps the stored windows, reconciled with OpenViews; Classic never sends them. Null members are left out
    // of the JSON so a pre-window update keeps the replay digest its receipt holds.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkspaceWindow[]? Windows { get; init; }
    // Null keeps the stored favourites.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkspaceViewRef[]? Favourites { get; init; }
}
