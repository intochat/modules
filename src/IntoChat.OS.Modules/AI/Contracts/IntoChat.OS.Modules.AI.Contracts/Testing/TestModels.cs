using System;
using System.Collections.Generic;

namespace IntoChat.OS.Modules.AI.Testing;

public sealed record TestExecutionRequest(
    string SuiteName,
    string? Filter = null,
    int TimeoutSeconds = 120,
    string? Configuration = null);

public sealed record TestFailureDetail(
    string TestName,
    string ErrorMessage,
    string? StackTrace = null);

public sealed record TestExecutionResult(
    bool Success,
    int Total,
    int Passed,
    int Failed,
    int Skipped,
    long DurationMs,
    string Summary,
    IReadOnlyList<TestFailureDetail> Failures);

public sealed record TestResultSummary(
    string RunId,
    string SuiteName,
    DateTimeOffset ExecutedAt,
    bool Success,
    int Total,
    int Passed,
    int Failed,
    long DurationMs);
