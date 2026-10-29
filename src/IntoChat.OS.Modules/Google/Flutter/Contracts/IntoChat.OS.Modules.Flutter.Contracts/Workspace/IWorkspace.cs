using DigitalBrain.Contracts;


namespace IntoChat.OS.Modules.Flutter.Workspace;

[NeuronAlias("ui.workspace"), DefaultNeuronType("ui.workspace")]
public interface IWorkspace
{
    const string Type = "ui.workspace";

    // The brain's one workspace (R4-19 singleton key; clean break from the bare brain key).
    static string Key(string brainId) => $"{brainId}/main";

    Task<WorkspaceState> Apply(WorkspaceUpdate next, string operationId, long expectedRevision);
    Task<WorkspaceState> Show(string app, string operationId, long expectedRevision);
    Task<WorkspaceState> Back(string operationId, long expectedRevision);
    Task<WorkspaceState> Home(string operationId, long expectedRevision);
    Task<WorkspaceState> Close(string app, string operationId, long expectedRevision);
    [DigitalBrain.ReadOnlyCall] Task<WorkspaceState> Read();
}
