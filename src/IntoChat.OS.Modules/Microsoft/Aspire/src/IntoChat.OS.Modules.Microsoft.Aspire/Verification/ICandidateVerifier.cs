namespace IntoChat.OS.Modules.Microsoft.Aspire.Verification;

internal interface ICandidateVerifier
{
    Task<VerificationReport> Verify(string brainId, string sha, CandidateEndpoints endpoints, VerificationPlan plan, CancellationToken ct);
}
