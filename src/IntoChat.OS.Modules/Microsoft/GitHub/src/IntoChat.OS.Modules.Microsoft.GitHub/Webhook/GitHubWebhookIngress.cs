using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IntoChat.OS.Connections.Platform.Connections;
using DigitalBrain.Runtime.Orleans.Hosting;

namespace IntoChat.OS.Modules.Microsoft.GitHub.Webhook;

internal sealed class GitHubWebhookIngress(GitHubAppRegistration registration, IServiceProvider plain)
{
    public async Task<GitHubWebhookAcceptance> HandleAsync(ReadOnlyMemory<byte> body,
        IReadOnlyDictionary<string, string[]> headers, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var delivery = Header(headers, "X-GitHub-Delivery");
        var eventName = Header(headers, "X-GitHub-Event");
        if (!Guid.TryParse(delivery, out _) || string.IsNullOrWhiteSpace(eventName))
        {
            return GitHubWebhookAcceptance.BadRequest;
        }
        try
        {
            using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 32 });
            var payload = document.RootElement;
            if (payload.ValueKind != JsonValueKind.Object)
            {
                return GitHubWebhookAcceptance.BadRequest;
            }
            var installation = RoutingHint(payload, "installation", "id");
            var repository = RoutingHint(payload, "repository", "id");
            var app = RoutingHint(payload, "hook", "app_id");
            var webhookSecret = await registration.ReadOptionalSecretAsync("WebhookSecret", cancellationToken);
            if (webhookSecret is not { Length: >= 16 }
                || !ValidateSignature(body.Span, Header(headers, "X-Hub-Signature-256"), webhookSecret))
            {
                return GitHubWebhookAcceptance.Unauthorized;
            }
            if (installation is null)
            {
                // An App-level ping proves the signed delivery path; no connection is addressed.
                if (eventName != "ping") { return GitHubWebhookAcceptance.Ignored; }
                return app is null || app == await registration.ReadAppIdAsync(cancellationToken)
                    ? GitHubWebhookAcceptance.Accepted : GitHubWebhookAcceptance.Unauthorized;
            }
            // Refresh no longer targets a single SHA because the receipt carries only the body hash.
            if (RejectCheckPayloadWithInvalidHeadSha(payload, eventName))
            {
                return GitHubWebhookAcceptance.BadRequest;
            }
            var hash = Convert.ToHexStringLower(SHA256.HashData(body.Span));
            var results = new List<GitHubWebhookAcceptance>();
            // Every connection of every brain on this installation; one target's failure never stops the rest.
            foreach (var key in await SiloNeurons.Get<IGitHubRepositoryIndex>(plain, IGitHubRepositoryIndex.Key(installation.Value)).Read())
            {
                using var activity = GitHubTelemetry.Source.StartActivity("github.webhook.accept", ActivityKind.Producer);
                try
                {
                    var target = SiloNeurons.Get<IGitHubInbound>(plain, key);
                    if (await target.Coordinates().ConfigureAwait(false) is not { } coordinates
                        || repository is not null && coordinates.RepositoryId != repository)
                    {
                        continue;
                    }
                    var classification = GitHubWebhookHandler.Classify(coordinates, payload, eventName);
                    if (classification.Failure is { } failure)
                    {
                        results.Add(failure);
                        continue;
                    }
                    GitHubTelemetry.TagReceipt(activity, coordinates, delivery!, classification.Number);
                    var fact = new RepositoryEvent(delivery!, eventName,
                        classification.Revoke ? "revoked" : GitHubWebhookHandler.String(payload, "action"),
                        classification.Number, hash, installation, repository, app);
                    var accepted = await target.AcceptRepositoryEvent(fact, cancellationToken).ConfigureAwait(false);
                    results.Add(accepted ? GitHubWebhookAcceptance.Accepted : GitHubWebhookAcceptance.Duplicate);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    GitHubTelemetry.Failed(activity, error);
                    results.Add(GitHubWebhookAcceptance.Unavailable);
                }
            }
            // Acknowledge only when every intended target durably accepted its delivery; a redelivery
            // reaches the targets that already accepted it as a duplicate.
            foreach (var failure in new[] { GitHubWebhookAcceptance.Unavailable, GitHubWebhookAcceptance.Unauthorized, GitHubWebhookAcceptance.BadRequest })
            {
                if (results.Contains(failure))
                {
                    return failure;
                }
            }
            return results.Contains(GitHubWebhookAcceptance.Accepted) ? GitHubWebhookAcceptance.Accepted
                : results.Contains(GitHubWebhookAcceptance.Duplicate) ? GitHubWebhookAcceptance.Duplicate : GitHubWebhookAcceptance.Ignored;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException)
        {
            return GitHubWebhookAcceptance.BadRequest;
        }
    }

    private static bool RejectCheckPayloadWithInvalidHeadSha(JsonElement payload, string eventName)
    {
        var sha = eventName switch
        {
            "check_run" => GitHubWebhookHandler.String(payload, "check_run", "head_sha"),
            "check_suite" => GitHubWebhookHandler.String(payload, "check_suite", "head_sha"),
            "status" => GitHubWebhookHandler.String(payload, "sha"),
            _ => null,
        };
        return sha is not null && !GitHubRepositorySource.IsSha(sha);
    }

    private static long? RoutingHint(JsonElement payload, string parent, string name)
        => payload.TryGetProperty(parent, out var value) && value.TryGetProperty(name, out var number) ? number.GetInt64() : null;

    private static bool ValidateSignature(ReadOnlySpan<byte> body, string? signature, string secret)
    {
        if (signature is not { Length: 71 } || !signature.StartsWith("sha256=", StringComparison.Ordinal))
        {
            return false;
        }
        byte[] supplied;
        try
        {
            supplied = Convert.FromHexString(signature.AsSpan(7));
        }
        catch (FormatException)
        {
            return false;
        }
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

    private static string? Header(IReadOnlyDictionary<string, string[]> headers, string name)
    {
        var matches = headers.Where(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 && matches[0].Value.Length == 1 ? matches[0].Value[0] : null;
    }
}
