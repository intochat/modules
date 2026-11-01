using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IntoChat.OS.Modules.AI.Testing;

public interface ITestVerificationService
{
    Task<TestExecutionResult> RunTargetedSuiteAsync(TestExecutionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TestResultSummary>> GetHistoricalRunsAsync(string testSuiteId, CancellationToken cancellationToken = default);
}
