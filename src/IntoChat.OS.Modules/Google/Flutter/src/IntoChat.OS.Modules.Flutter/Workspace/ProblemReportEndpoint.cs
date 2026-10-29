using IntoChat.OS.Runtime;
using DigitalBrain.Spi;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using Microsoft.AspNetCore.Http;

namespace IntoChat.OS.Modules.Flutter.Workspace;

internal static class ProblemReportEndpoint
{
    public static async Task<IResult> File(ProblemReportInput input, INeuronProxies brain, CancellationToken ct)
    {
        if (!BrainScope.IsValidId(input.IntentId) || string.IsNullOrWhiteSpace(input.Message))
        { return Results.BadRequest(new { error = "A report needs the intent id it came from and a message." }); }
        try
        {
            var scope = BrainScope.CurrentId();
            var report = await brain.Get<IProblemReports>(scope + "/main").Add(scope, input.IntentId!, input.Message!.Trim()).WaitAsync(ct);
            return Results.Created($"/brains/{scope}/reports", report);
        }
        catch (ArgumentException error) { return Results.BadRequest(new { error = error.Message }); }
    }
}
