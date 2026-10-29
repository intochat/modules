using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Flutter.Workspace.Signals;

[Signal("ui.workspace-changed")]
public sealed record WorkspaceChanged(
    string WorkspaceId,
    long Revision) : DigitalBrain.Signal;
