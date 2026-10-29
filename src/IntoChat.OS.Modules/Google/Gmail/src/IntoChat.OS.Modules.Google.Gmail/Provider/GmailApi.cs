using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IntoChat.OS.Contracts.Compute;
using IntoChat.OS.Framework.Modules.Metering;
using IntoChat.OS.Modules.Google.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntoChat.OS.Modules.Google.Gmail;

internal sealed class GmailApi(IHttpClientFactory clients, GmailModuleOptions options, TimeProvider clock, ILogger<GmailApi>? logger = null) : IGmailApi
{
    private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(30);
    private readonly ILogger<GmailApi> _logger = logger ?? NullLogger<GmailApi>.Instance;

    public Task<GoogleApiResult<GoogleProfile>> ProfileAsync(string accessToken, GoogleCallContext context, CancellationToken ct)
        => Send(accessToken, context, GoogleOperations.Profile, options.ProfileEndpoint, null, true,
            root => new GoogleProfile(GoogleApiReader.Required(root, "emailAddress"), GoogleApiReader.HistoryId(root)), ct);

    public Task<GoogleApiResult<GoogleMessagePage>> ListMessagesAsync(string accessToken, GoogleCallContext context, int take, string? pageToken, CancellationToken ct)
    {
        CheckPage(take, pageToken);
        return Send(accessToken, context, GoogleOperations.ListMessages,
            Endpoint($"messages?labelIds=INBOX&maxResults={take}" + Page(pageToken)), null, true,
            root => GoogleApiReader.MessagePage(root, take), ct);
    }

    public Task<GoogleApiResult<GoogleMessageMetadata>> ReadMetadataAsync(string accessToken, GoogleCallContext context, string messageId, CancellationToken ct)
        => Send(accessToken, context, GoogleOperations.Metadata, MessageEndpoint(messageId, "metadata"), null, true, GoogleApiReader.Metadata, ct);

    public Task<GoogleApiResult<GoogleMessageContent>> ReadMessageAsync(string accessToken, GoogleCallContext context, string messageId, CancellationToken ct)
        => Send(accessToken, context, GoogleOperations.Message, MessageEndpoint(messageId, "full"), null, true, GoogleApiReader.Message, ct);

    public Task<GoogleApiResult<GoogleWatch>> WatchAsync(string accessToken, GoogleCallContext context, string topic, CancellationToken ct)
        => Send(accessToken, context, GoogleOperations.Watch, Endpoint("watch"),
            () => JsonContent.Create(new { topicName = topic, labelIds = new[] { "INBOX" }, labelFilterBehavior = "INCLUDE" }), false,
            GoogleApiReader.Watch, ct);

    public Task<GoogleApiResult<GoogleUnit>> StopAsync(string accessToken, GoogleCallContext context, CancellationToken ct)
        => Send(accessToken, context, GoogleOperations.Stop, Endpoint("stop"), null, false, _ => GoogleUnit.Value, ct, HttpMethod.Post, readBody: false);

    public Task<GoogleApiResult<GoogleHistoryPage>> HistoryAsync(string accessToken, GoogleCallContext context, string startHistoryId, string? pageToken, int take, CancellationToken ct)
    {
        CheckPage(take, pageToken);
        var start = GoogleApiReader.ParseHistoryId(startHistoryId);
        return Send(accessToken, context, GoogleOperations.History,
            Endpoint($"history?startHistoryId={Uri.EscapeDataString(startHistoryId)}&maxResults={take}" + Page(pageToken)), null, true,
            root => GoogleApiReader.History(root, start, take), ct);
    }

    public async Task<GoogleDraftWriteResult> CreateDraftAsync(string accessToken, GoogleCallContext context, byte[] mime, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mime);
        if (ct.IsCancellationRequested) { return new(GoogleWriteOutcome.NotDispatched, CancellationRequested: true); }
        var raw = Convert.ToBase64String(mime).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        using var client = clients.CreateClient(GoogleHttp.ClientName);
        using var request = Request(accessToken, context, GoogleOperations.CreateDraft, Endpoint("drafts"), HttpMethod.Post,
            JsonContent.Create(new { message = new { raw } }));
        using var deadline = new CancellationTokenSource(ProviderTimeout, clock);
        using var dispatch = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        ExternalCallCompletion? accounting = null;
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, dispatch.Token);
            accounting = response.GetExternalAccounting();
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                return new(status is 400 or 401 or 403 or 404 or 429 ? GoogleWriteOutcome.Rejected : GoogleWriteOutcome.Unknown,
                    Failure: await Failure(response, deadline.Token), Accounting: accounting, CancellationRequested: ct.IsCancellationRequested);
            }
            // Recover a returned identity after caller cancellation without resending the mutation.
            using var json = await GoogleApiReader.Read(response.Content, deadline.Token);
            var id = GoogleApiReader.Required(json.RootElement, "id");
            var message = json.RootElement.TryGetProperty("message", out var value) && value.ValueKind == JsonValueKind.Object
                ? GoogleApiReader.Optional(value, "id") : null;
            return new(GoogleWriteOutcome.Succeeded, id, message, Accounting: accounting, CancellationRequested: ct.IsCancellationRequested);
        }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or JsonException or GoogleInvalidResponseException)
        {
            _logger.LogWarning("Google draft outcome is unknown after {FailureType}.", error.GetType().Name);
            return new(GoogleWriteOutcome.Unknown, Failure: new(GoogleApiFailureKind.Unavailable), Accounting: accounting, CancellationRequested: ct.IsCancellationRequested);
        }
    }

    private async Task<GoogleApiResult<T>> Send<T>(string accessToken, GoogleCallContext context, string operation, Uri uri,
        Func<HttpContent>? content, bool retry, Func<JsonElement, T> parse, CancellationToken ct, HttpMethod? method = null, bool readBody = true) where T : class
    {
        ct.ThrowIfCancellationRequested();
        using var deadline = new CancellationTokenSource(ProviderTimeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        var until = clock.GetUtcNow() + ProviderTimeout;
        GoogleApiFailure? last = null;
        ExternalCallCompletion? accounting = null;
        for (var attempt = 0; attempt < (retry ? 3 : 1); attempt++)
        {
            try
            {
                using var client = clients.CreateClient(GoogleHttp.ClientName);
                using var request = Request(accessToken, context, operation, uri, method ?? (content is null ? HttpMethod.Get : HttpMethod.Post), content?.Invoke());
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token);
                accounting = response.GetExternalAccounting();
                if (response.IsSuccessStatusCode)
                {
                    if (!readBody) { return new(parse(default), null, accounting); }
                    using var json = await GoogleApiReader.Read(response.Content, linked.Token);
                    return new(parse(json.RootElement), null, accounting);
                }
                last = await Failure(response, linked.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (OperationCanceledException) { return new(null, new(GoogleApiFailureKind.Unavailable, Retryable: true), accounting); }
            catch (Exception error) when (error is HttpRequestException or IOException)
            {
                _logger.LogDebug("Google read transport failed with {FailureType}.", error.GetType().Name);
                last = new(GoogleApiFailureKind.Unavailable, Retryable: true);
            }
            catch (Exception error) when (error is JsonException or GoogleInvalidResponseException)
            {
                _logger.LogWarning("Google read response was invalid ({FailureType}).", error.GetType().Name);
                return new(null, new(GoogleApiFailureKind.InvalidResponse), accounting);
            }

            if (!retry || attempt == 2 || !last.Retryable) { break; }
            var delay = TimeSpan.FromSeconds(attempt + 1);
            if (last.RetryAt is { } at && at - clock.GetUtcNow() > delay) { delay = at - clock.GetUtcNow(); }
            if (clock.GetUtcNow() + delay >= until) { break; }
            try { await Task.Delay(delay, clock, linked.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            { return new(null, new(GoogleApiFailureKind.Unavailable, Retryable: true), accounting); }
        }
        return new(null, last ?? new(GoogleApiFailureKind.Unavailable), accounting);
    }

    private async Task<GoogleApiFailure> Failure(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        string? reason = null;
        try
        {
            using var json = await GoogleApiReader.Read(response.Content, ct);
            if (json.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0 &&
                errors[0].ValueKind == JsonValueKind.Object)
            { reason = GoogleApiReader.Optional(errors[0], "reason"); }
        }
        catch (Exception error) when (error is JsonException or GoogleInvalidResponseException)
        { _logger.LogDebug("Google error response body was invalid ({FailureType}); using HTTP status.", error.GetType().Name); }
        var retryAt = response.Headers.RetryAfter?.Date ?? (response.Headers.RetryAfter?.Delta is { } delta ? clock.GetUtcNow() + delta : null);
        return status switch
        {
            400 => new(GoogleApiFailureKind.InvalidRequest, status),
            401 => new(GoogleApiFailureKind.ReconsentRequired, status),
            403 when reason is "rateLimitExceeded" or "userRateLimitExceeded" => new(GoogleApiFailureKind.RateLimited, status, retryAt, true),
            403 when reason == "dailyLimitExceeded" => new(GoogleApiFailureKind.RateLimited, status, retryAt),
            403 => new(GoogleApiFailureKind.Forbidden, status),
            404 => new(GoogleApiFailureKind.NotFound, status),
            429 => new(GoogleApiFailureKind.RateLimited, status, retryAt, true),
            500 or 502 or 503 or 504 => new(GoogleApiFailureKind.Unavailable, status, retryAt, true),
            _ => new(GoogleApiFailureKind.Unavailable, status),
        };
    }

    private static HttpRequestMessage Request(string accessToken, GoogleCallContext context, string operation, Uri uri, HttpMethod method, HttpContent? content)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.SetExternalCall(new(operation, context.Funding, context.Subject, context.InvocationId));
        return request;
    }

    private Uri Endpoint(string path) => new(options.GmailEndpoint, path);
    private Uri MessageEndpoint(string id, string format)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 1024) { throw new ArgumentException("A bounded message ID is required.", nameof(id)); }
        return Endpoint($"messages/{Uri.EscapeDataString(id)}?format={format}");
    }
    private static string Page(string? token) => token is null ? "" : "&pageToken=" + Uri.EscapeDataString(token);
    private static void CheckPage(int take, string? token)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(take, 100);
        if (token is { Length: > 8192 }) { throw new ArgumentException("The provider page token is too large.", nameof(token)); }
    }
}
