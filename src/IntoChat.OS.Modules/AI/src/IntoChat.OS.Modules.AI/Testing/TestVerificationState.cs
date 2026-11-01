namespace IntoChat.OS.Modules.AI.Testing;

[GenerateSerializer, Alias("testing.verification-state")]
public sealed class TestVerificationState
{
    [Id(0)] public string SuiteId { get; set; } = string.Empty;
    [Id(1)] public TestResultSummary[] Runs { get; set; } = [];
}
