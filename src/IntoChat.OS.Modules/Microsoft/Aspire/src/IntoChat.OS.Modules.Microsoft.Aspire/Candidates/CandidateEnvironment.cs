namespace IntoChat.OS.Modules.Microsoft.Aspire;

/// <summary>Environment variable names a contributor candidate reads, shared with the module's Aspire hosting.</summary>
public static class CandidateEnvironment
{
    /// <summary>Carries the Aspire dashboard OTLP HTTP endpoint to a candidate.</summary>
    public const string OtlpHttpEndpoint = "ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL";
}
