using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Workspace;

[GenerateSerializer, Alias("ui.problem-reports-state")]
[ExportableState]
public sealed record ProblemReportsState
{
    [Id(0)] public ProblemReport[] Reports { get; init; } = [];
}
