namespace IntoChat.OS.Modules.Microsoft.Aspire;

public sealed record NeuronExpectation(
    string Contract,
    string Key,
    string Method,
    string Arguments,
    string Expected);

// Written by the change's author: what to ask the candidate and what it must answer. The budget is
// the plan's, never a global timeout.
public sealed record VerificationPlan(
    NeuronExpectation[] Calls,
    string[] Spans,
    TimeSpan Budget);

public sealed record VerificationCheck(string Name, bool Passed, string Detail);

public sealed record VerificationReport(VerificationCheck[] Checks, TimeSpan Elapsed)
{
    public bool Passed => Checks.All(check => check.Passed);
    public string? FirstFailure => Checks.FirstOrDefault(check => !check.Passed)?.Name;
}
