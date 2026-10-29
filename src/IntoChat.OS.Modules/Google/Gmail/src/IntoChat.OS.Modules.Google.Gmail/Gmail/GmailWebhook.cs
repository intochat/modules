using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Connections.Platform.Connections;
using IntoChat.OS.Connections.Platform.Identity;
using IntoChat.OS.Framework.Modules.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using DigitalBrain.Runtime.Orleans.Hosting;

namespace IntoChat.OS.Modules.Google.Gmail;

internal static class GmailWebhook
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(GmailModule.WatchPath + "/{brainId}", async (string brainId, HttpRequest request,
            IGmailPushAuthenticator authenticator, IBrainOwnerDispatch dispatch, IServiceProvider plain, ILogger<GmailModule> logger, CancellationToken ct) =>
        {
            if (!await authenticator.AuthenticateAsync(request, ct)) { return Results.Unauthorized(); }
            if (!BrainScope.IsValidId(brainId)) { return Results.NotFound(); }
            var (envelope, failure) = await HttpProblems.ReadValidatedAsync(request, new GmailPubSubValidator(), GmailPubSub.MaxEnvelopeBytes, ct);
            if (failure is not null) { return failure; }
            if (!GmailPubSub.TryUnwrap(envelope!, out var push)) { return Results.BadRequest(); }
            var accepted = false;
            var duplicate = false;
            var retryable = false;
            var found = await dispatch.RunAsync(brainId, brainId + "/gmail-push", async () =>
            {
                foreach (var key in await SiloNeurons.Get<IConnectionIndex>(plain, IConnectionIndex.Key(brainId)).List())
                {
                    if (ConnectionKeys.Parse(key).ConnectorId != GmailModule.Gmail.Id) { continue; }
                    try
                    {
                        await dispatch.RunAsync(brainId, key, async () =>
                        {
                            var result = await SiloNeurons.Get<IGmailInbound>(plain, key).AcceptWatchPush(push);
                            accepted |= result == GmailPushAcceptance.Accepted;
                            duplicate |= result == GmailPushAcceptance.Duplicate;
                            retryable |= result == GmailPushAcceptance.Retryable;
                        }, cancellationToken: ct);
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    {
                        logger.LogWarning("Gmail push delivery could not be completed ({FailureType}).", error.GetType().Name);
                        retryable = true;
                    }
                }
            }, cancellationToken: ct);
            if (!found) { return Results.NotFound(); }
            return retryable ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
                : accepted ? Results.Accepted() : duplicate ? Results.Ok() : Results.NoContent();
        }).AllowAnonymous();
    }
}
