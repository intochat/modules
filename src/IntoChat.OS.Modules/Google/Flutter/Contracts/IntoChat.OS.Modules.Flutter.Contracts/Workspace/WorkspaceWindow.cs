namespace IntoChat.OS.Modules.Flutter.Workspace;

// A desktop window: its tabs are open views, and the tab id is the view id.
public sealed record WorkspaceWindow(string Id, WorkspaceViewRef[] Tabs,
    string ActiveTabId);
