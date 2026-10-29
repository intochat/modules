using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Workspace;

[Neuron("ui.problem-reports", State = "problem-reports")]
internal sealed class ProblemReportsNeuron
    : DigitalBrain.Neuron<ProblemReportsState>, IProblemReports
{
    private const int MaxMessageLength = 4_000;

    public async Task<ProblemReport> Add(string workspaceId, string intentId, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (message.Length > MaxMessageLength)
        {
            throw new ArgumentException($"A problem report is limited to {MaxMessageLength} characters.", nameof(message));
        }

        var report = new ProblemReport(intentId, workspaceId, message, DateTimeOffset.UtcNow);
        await Save(State with { Reports = [.. State.Reports, report] }, new ProblemReported(workspaceId, intentId));
        return report;
    }

    public Task<IReadOnlyList<ProblemReport>> Read() => Task.FromResult<IReadOnlyList<ProblemReport>>([.. State.Reports]);
}

// A report was filed: the saved change says which intent it came from (never the message itself).
[Signal("ui.problem-reported")]
public sealed record ProblemReported(string WorkspaceId, string IntentId) : DigitalBrain.Signal;
