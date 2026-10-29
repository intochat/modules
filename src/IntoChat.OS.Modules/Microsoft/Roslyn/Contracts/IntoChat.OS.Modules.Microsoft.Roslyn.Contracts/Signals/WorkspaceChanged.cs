using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Microsoft.Roslyn;

[Signal("coding.workspace-changed")]
public sealed record WorkspaceChanged(
    string Key,
    long Generation,
    WorkspacePhase Phase) : DigitalBrain.Signal;
