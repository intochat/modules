using System.Diagnostics;
using System.Text.Json;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Modules.Mcp;
using IntoChat.OS.Modules.Microsoft.Aspire.Deployment;
using Microsoft.Extensions.Logging;

namespace IntoChat.OS.Modules.Microsoft.Aspire.Verification;

// The developer's loop as a neuron: the candidate's MCP answers for the same brain, its apps
// activate from the copied state, every expectation answers as written, every span appears.
internal sealed class CandidateVerifier(IIntoChatOSBrains brains, IClusterHost cluster, ILogger<CandidateVerifier> logger) : ICandidateVerifier
{
    private const string AppContract = "IntoChat.OS.Modules.Apps.IApp";
    private const string ReconcilerContract = "IntoChat.OS.Modules.Apps.IFeatureReconciler";

    private sealed record Check(string Name, Func<CancellationToken, Task<(bool Passed, string Detail)>> Run);

    public async Task<VerificationReport> Verify(string brainId, string sha, CandidateEndpoints endpoints, VerificationPlan plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(plan);
        var clock = Stopwatch.StartNew();
        var server = brains.Get<IMcpServer>($"{brainId}/mcp/candidate-{sha}");
        List<VerificationCheck> checks = [];
        try { await server.Connect(new($"candidate {sha}", $"{endpoints.Mcp}/brains/{brainId}/mcp"), ct); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            checks.Add(new("connect", false, error.Message));
            return new([.. checks], clock.Elapsed);
        }
        try
        {
            foreach (var check in Checks(brainId, sha, plan, server))
            {
                VerificationCheck outcome;
                try
                {
                    var (passed, detail) = await check.Run(ct);
                    outcome = new(check.Name, passed, detail);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                { outcome = new(check.Name, false, error.Message); }
                checks.Add(outcome);
                if (!outcome.Passed) { break; }
            }
        }
        finally
        {
            try { await server.Disconnect(CancellationToken.None); }
            catch (Exception error) when (error is not OperationCanceledException)
            { logger.LogWarning(error, "Disconnecting candidate {Sha}'s MCP server failed", sha); }
        }
        return new([.. checks], clock.Elapsed);
    }

    private IEnumerable<Check> Checks(string brainId, string sha, VerificationPlan plan, IMcpServer server)
    {
        yield return new("context", async ct =>
        {
            var context = await server.Run("neurons_context", "{}", ct);
            if (context.IsError) { return (false, context.Text); }
            using var document = JsonDocument.Parse(context.Text);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("brainId", out var id) || id.ValueKind != JsonValueKind.String)
            { return (false, $"no brainId in the candidate's answer: {context.Text}"); }
            var answered = id.GetString();
            return (answered == brainId, $"candidate answers for brain '{answered}'");
        });
        yield return new("activation", async ct =>
        {
            // Features reconcile on the brain's activation without anyone waiting: reconciling here makes the
            // candidate's apps the folder's versions before any plan call reads them.
            var reconcile = JsonSerializer.Serialize(new { contract = ReconcilerContract, key = $"{brainId}/apps-reconciler", method = "Reconcile", arguments = "[]" });
            var reconciled = await server.Run("neurons_call", reconcile, ct);
            if (reconciled.IsError) { return (false, $"reconciling features failed: {reconciled.Text}"); }
            var apps = await server.Run("neurons_list", JsonSerializer.Serialize(new { contract = AppContract }), ct);
            return (!apps.IsError, apps.IsError ? apps.Text : "the brain's apps list from the copied state");
        });
        foreach (var call in plan.Calls)
        {
            yield return new($"call {call.Contract} {call.Key} {call.Method}", async ct =>
            {
                // neurons_call takes the arguments as one JSON array string; a plan that is not an array fails here, not remotely.
                using (var arguments = JsonDocument.Parse(call.Arguments))
                {
                    if (arguments.RootElement.ValueKind != JsonValueKind.Array) { return (false, $"arguments must be a JSON array: {call.Arguments}"); }
                }
                var body = JsonSerializer.Serialize(new { contract = call.Contract, key = call.Key, method = call.Method, arguments = call.Arguments });
                var answer = await server.Run("neurons_call", body, ct);
                var passed = !answer.IsError && (call.Expected == "*" || answer.Text.Contains(call.Expected, StringComparison.Ordinal));
                return (passed, passed ? answer.Text : $"expected {call.Expected}; answered {answer.Text}");
            });
        }
        foreach (var span in plan.Spans)
        {
            yield return new($"span {span}", async ct =>
            {
                var count = await cluster.Spans($"candidate-{sha}", span, ct);
                return (count > 0, $"{count} span(s)");
            });
        }
    }
}
