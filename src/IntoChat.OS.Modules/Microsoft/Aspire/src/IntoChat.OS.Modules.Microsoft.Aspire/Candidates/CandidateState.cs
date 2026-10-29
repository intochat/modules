using DigitalBrain.State;

namespace IntoChat.OS.Modules.Microsoft.Aspire.Candidates;

[ExportableState]
internal sealed record CandidateState
{
    public string Branch { get; init; } = "";
    public string Summary { get; init; } = "";
    public CandidateStage Stage { get; init; } = CandidateStage.Proposed;
    public CandidateEndpoints? Endpoints { get; init; }
    public VerificationPlan? Plan { get; init; }
    public VerificationReport? Report { get; init; }
    public string? Failure { get; init; }
    public CandidateStage[] History { get; init; } = [];
}
