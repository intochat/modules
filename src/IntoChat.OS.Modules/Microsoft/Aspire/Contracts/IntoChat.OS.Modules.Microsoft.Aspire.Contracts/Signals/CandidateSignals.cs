namespace IntoChat.OS.Modules.Microsoft.Aspire;

// [Signal] records since 06-06d (the candidates are plain neurons); aliases unchanged.
[DigitalBrain.Signal("aspire.candidate.changed")] public sealed record CandidateChanged(string Sha, CandidateStage Stage) : DigitalBrain.Signal;
[DigitalBrain.Signal("aspire.candidate.started")] public sealed record CandidateStarted(string Sha, CandidateEndpoints Endpoints) : DigitalBrain.Signal;
[DigitalBrain.Signal("aspire.candidate.healthy")] public sealed record CandidateHealthy(string Sha) : DigitalBrain.Signal;
[DigitalBrain.Signal("aspire.candidate.verified")] public sealed record CandidateVerified(string Sha, VerificationReport Report) : DigitalBrain.Signal;
[DigitalBrain.Signal("aspire.candidate.promoted")] public sealed record CandidatePromoted(string Sha) : DigitalBrain.Signal;
[DigitalBrain.Signal("aspire.candidate.failed")] public sealed record CandidateFailed(string Sha, CandidateStage Stage, string Reason) : DigitalBrain.Signal;
