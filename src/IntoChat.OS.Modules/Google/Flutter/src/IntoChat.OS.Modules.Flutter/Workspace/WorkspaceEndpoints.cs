using DigitalBrain;
using IntoChat.OS.Runtime;
using DigitalBrain.Spi;
using IntoChat.OS.Connections;
using IntoChat.OS.Framework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace IntoChat.OS.Modules.Flutter.Workspace;

internal static class WorkspaceEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var brain = BrainRoutes.Group(endpoints);
        brain.MapGet("", (INeuronProxies intoChatOS, CancellationToken ct)
            => Respond(async () => Results.Ok(await Workspace(intoChatOS).Read().WaitAsync(ct))));
        brain.MapPost("/show", (NeuronCommand input, INeuronProxies intoChatOS, CancellationToken ct)
            => Respond(async () => Results.Ok(await Workspace(intoChatOS).Show(input.App, input.OperationId, input.ExpectedRevision).WaitAsync(ct))));
        brain.MapPost("/back", (NavigationCommand input, INeuronProxies intoChatOS, CancellationToken ct)
            => Respond(async () => Results.Ok(await Workspace(intoChatOS).Back(input.OperationId, input.ExpectedRevision).WaitAsync(ct))));
        brain.MapPost("/home", (NavigationCommand input, INeuronProxies intoChatOS, CancellationToken ct)
            => Respond(async () => Results.Ok(await Workspace(intoChatOS).Home(input.OperationId, input.ExpectedRevision).WaitAsync(ct))));
        brain.MapPost("/close", (NeuronCommand input, INeuronProxies intoChatOS, CancellationToken ct)
            => Respond(async () => Results.Ok(await Workspace(intoChatOS).Close(input.App, input.OperationId, input.ExpectedRevision).WaitAsync(ct))));
        brain.MapPost("/reports", (ProblemReportInput input, INeuronProxies intoChatOS, CancellationToken ct) => ProblemReportEndpoint.File(input, intoChatOS, ct));
    }

    private static IWorkspace Workspace(INeuronProxies intoChatOS) => intoChatOS.Get<IWorkspace>(IWorkspace.Key(BrainScope.CurrentId()));

    private static async Task<IResult> Respond(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (RevisionConflictException error) { return Results.Conflict(new { error = error.Message, currentRevision = error.CurrentRevision }); }
        catch (InvalidOperationException error) { return Results.Conflict(new { error = error.Message }); }
        catch (KeyNotFoundException) { return Results.NotFound(new { error = "The requested workspace item was not found." }); }
        catch (ArgumentException error) { return Results.BadRequest(new { error = error.Message }); }
        catch (TimeoutException) { return Results.Problem("The workspace operation timed out. Refresh before retrying.", statusCode: 503); }
    }

    internal sealed record NeuronCommand(string App, string OperationId, long ExpectedRevision);
    internal sealed record NavigationCommand(string OperationId, long ExpectedRevision);
}
