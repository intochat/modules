namespace IntoChat.OS.Modules.Flutter.Workspace;

// Derived windows always satisfy Apply's rules: ids are w-<n>, at most MaxWindows windows of at most MaxTabs
// tabs. Tab ids are view ids, which CheckViews already bounds and makes unique, so tabs need no mapping.
internal static class WorkspaceWindows
{
    public const int MaxWindows = 8, MaxTabs = 8;

    public static WorkspaceWindow[] Derive(WorkspaceViewRef[] openViews) => Place([], openViews);

    // Keeps each window's tabs that are still open, refreshed from OpenViews, and places every open view no
    // window holds.
    public static WorkspaceWindow[] Reconcile(WorkspaceWindow[] windows, WorkspaceViewRef[] openViews)
    {
        var open = openViews.ToDictionary(view => view.Id, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var kept = windows
            .Where(window => window is not null)
            .Select(window => window with
            {
                Tabs = [.. (window.Tabs ?? []).Where(tab => tab is not null && open.ContainsKey(tab.Id) && seen.Add(tab.Id))
                    .Select(tab => open[tab.Id]).Take(MaxTabs)],
            })
            .Where(window => window.Tabs.Length > 0)
            .Select(window => window.Tabs.Any(tab => tab.Id == window.ActiveTabId) ? window : window with { ActiveTabId = window.Tabs[^1].Id })
            .Take(MaxWindows)
            .ToList();
        var held = kept.SelectMany(window => window.Tabs).Select(tab => tab.Id).ToHashSet(StringComparer.Ordinal);
        return Place(kept, [.. openViews.Where(view => !held.Contains(view.Id))]);
    }

    // A new single-tab window per view while there is room for windows, then into the first window with room.
    private static WorkspaceWindow[] Place(List<WorkspaceWindow> windows, WorkspaceViewRef[] views)
    {
        var ids = windows.Select(window => window.Id).ToHashSet(StringComparer.Ordinal);
        var next = 1;
        foreach (var view in views)
        {
            if (windows.Count < MaxWindows)
            {
                while (!ids.Add("w-" + next)) { next++; }
                windows.Add(new("w-" + next, [view], view.Id));
                continue;
            }
            var at = windows.FindIndex(window => window.Tabs.Length < MaxTabs);
            if (at < 0) { break; }
            windows[at] = windows[at] with { Tabs = [.. windows[at].Tabs, view] };
        }
        return [.. windows];
    }
}
