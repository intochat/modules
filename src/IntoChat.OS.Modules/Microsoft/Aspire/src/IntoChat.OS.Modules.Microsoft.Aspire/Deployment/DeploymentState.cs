using DigitalBrain.State;

namespace IntoChat.OS.Modules.Microsoft.Aspire.Deployment;

[GenerateSerializer, Alias("aspire.deployment.state")]
[ExportableState]
internal sealed record DeploymentState
{
    [Id(0)] public string CurrentSha { get; init; } = "";
    [Id(1)] public DeploymentStage Stage { get; init; } = DeploymentStage.Steady;
    [Id(2)] public string? TargetSha { get; init; }
    // Rolling or RollingBack: the instances still to move onto TargetSha, in roll order, and those already moved.
    [Id(3)] public string[] Remaining { get; init; } = [];
    [Id(4)] public string[] Rolled { get; init; } = [];
    // The build the roll leaves; a roll back swaps it with TargetSha.
    [Id(5)] public string? PreviousSha { get; init; }
    // The silo whose own restart was issued detached; the next activation ends the roll once it is ready.
    [Id(6)] public string? Handover { get; init; }
    // Why a roll is going back (RollingBack) or went back (Steady after it).
    [Id(7)] public string? Failure { get; init; }
    // The instance this step drained: a resumed step waits for it to leave the front without draining it again.
    [Id(8)] public string? Draining { get; init; }
    // When the handed-over silo's restart was issued: its old process stopping before then is not a failed start.
    [Id(9)] public DateTimeOffset? HandoverAt { get; init; }
}
