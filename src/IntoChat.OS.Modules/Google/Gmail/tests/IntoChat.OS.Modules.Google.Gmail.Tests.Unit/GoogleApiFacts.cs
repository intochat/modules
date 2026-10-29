using System.Net;
using System.Text;
using System.Text.Json;
using IntoChat.OS.Contracts.Compute;
using IntoChat.OS.Modules.Google.Gmail;
using Microsoft.Extensions.Time.Testing;

namespace IntoChat.OS.Modules.Google.Gmail.Tests.Unit;

public sealed class GoogleApiFacts
{
    private static readonly GoogleCallContext Context = new(Funding.OwnConnection, new(Neuron: "brain/connection/gmail/mail"), "invocation");

    [Fact]
    public async Task ListReturnsOnlyBoundedProviderReferencesAndPreservesTheContinuation()
    {
        using var transport = new Transport(_ => Json("""{"messages":[{"id":"m1","threadId":"t1"}],"nextPageToken":"next +/"}"""));
        var result = await Api(transport).ListMessagesAsync("token", Context, 25, "page +/", TestContext.Current.CancellationToken);
        Assert.Null(result.Failure);
        Assert.Equal("m1", Assert.Single(result.Value!.Messages).Id);
        Assert.Equal("next +/", result.Value.NextPageToken);
        Assert.Contains("maxResults=25", transport.Uris[0].Query, StringComparison.Ordinal);
        Assert.Contains("labelIds=INBOX", transport.Uris[0].Query, StringComparison.Ordinal);
        Assert.Contains("pageToken=page%20%2B%2F", transport.Uris[0].Query, StringComparison.Ordinal);
        Assert.Equal("Bearer token", Assert.Single(transport.Authorization));
    }

    [Fact]
    public async Task ReadRetriesTransientResponsesButDoesNotRepeatPermanentRejections()
    {
        using var transient = new Transport(n => n < 3 ? Json("{}", HttpStatusCode.ServiceUnavailable) : Json("""{"emailAddress":"owner@example.test","historyId":"123"}"""));
        var result = await Api(transient).ProfileAsync("token", Context, TestContext.Current.CancellationToken);
        Assert.Equal("123", result.Value!.HistoryId);
        Assert.Equal(3, transient.Uris.Count);
        using var forbidden = new Transport(_ => Json("""{"error":{"errors":[{"reason":"domainPolicy","message":"PRIVATE"}]}}""", HttpStatusCode.Forbidden));
        var denied = await Api(forbidden).ProfileAsync("token", Context, TestContext.Current.CancellationToken);
        Assert.Equal(GoogleApiFailureKind.Forbidden, denied.Failure!.Kind);
        Assert.False(denied.Failure.Retryable);
        Assert.Single(forbidden.Uris);
        Assert.DoesNotContain("PRIVATE", denied.Failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AHistoryPageCannotExceedTheRequestedWindowOrRegressItsCursor()
    {
        using var transport = new Transport(_ => Json("""{"historyId":"9","history":[{"id":"9"}]}"""));
        var result = await Api(transport).HistoryAsync("token", Context, "10", null, 100, TestContext.Current.CancellationToken);
        Assert.Equal(GoogleApiFailureKind.InvalidResponse, result.Failure!.Kind);
        Assert.Single(transport.Uris);
    }

    [Theory]
    [InlineData("{}", 200, (int)GoogleWriteOutcome.Unknown)]
    [InlineData("not-json", 200, (int)GoogleWriteOutcome.Unknown)]
    [InlineData("{}", 503, (int)GoogleWriteOutcome.Unknown)]
    [InlineData("{}", 400, (int)GoogleWriteOutcome.Rejected)]
    [InlineData("{\"id\":\"draft1\",\"message\":{\"id\":\"m1\"}}", 200, (int)GoogleWriteOutcome.Succeeded)]
    public async Task DraftCreationMakesOneAttemptAndDistinguishesKnownAndUnknownOutcomes(string body, int status, int expected)
    {
        using var transport = new Transport(_ => Json(body, (HttpStatusCode)status));
        var result = await Api(transport).CreateDraftAsync("token", Context, Encoding.UTF8.GetBytes("Subject: sample\r\n\r\nbody"), TestContext.Current.CancellationToken);
        Assert.Equal((GoogleWriteOutcome)expected, result.Outcome);
        Assert.Single(transport.Uris);
        Assert.Equal("/gmail/drafts", transport.Uris[0].AbsolutePath);
        if (expected == (int)GoogleWriteOutcome.Succeeded) { Assert.Equal("draft1", result.DraftId); }
    }

    [Fact]
    public async Task BodyPartsHaveSeparateMediaTypesAndMissingDataIsExplicit()
    {
        using var transport = new Transport(_ => Json("""{"id":"m1","threadId":"t1","labelIds":["UNREAD"],"payload":{"mimeType":"multipart/alternative","parts":[{"partId":"0","mimeType":"text/plain","body":{"data":"aGVsbG8"}},{"partId":"1","mimeType":"text/html","body":{"attachmentId":"external","size":5}}]}}"""));
        var result = await Api(transport).ReadMessageAsync("token", Context, "m1", TestContext.Current.CancellationToken);
        var part = Assert.Single(result.Value!.BodyParts);
        Assert.Equal("text/plain", part.MimeType);
        Assert.Equal("hello", part.Content);
        Assert.Equal("attachment_data_not_fetched", Assert.Single(result.Value.Omissions).Reason);
        Assert.Single(transport.Uris);
    }

    [Fact]
    public async Task PaddedBase64UrlTextIsAccepted()
    {
        using var transport = new Transport(_ => Json("""{"id":"m","threadId":"t","payload":{"mimeType":"text/plain","body":{"data":"aGk="}}}"""));
        var result = await Api(transport).ReadMessageAsync("token", Context, "m", TestContext.Current.CancellationToken);
        Assert.Null(result.Failure);
        Assert.Equal("hi", Assert.Single(result.Value!.BodyParts).Content);
    }

    [Fact]
    public async Task ATraversalLimitReturnsAnExplicitOmissionRatherThanAnInvalidMessage()
    {
        var body = JsonSerializer.Serialize(new
        {
            id = "m",
            threadId = "t",
            payload = new
            {
                mimeType = "multipart/mixed",
                parts = Enumerable.Range(0, 260).Select(i => new { partId = i.ToString(System.Globalization.CultureInfo.InvariantCulture), mimeType = "text/plain", body = new { data = "YQ" } }).ToArray()
            }
        });
        using var transport = new Transport(_ => Json(body));
        var result = await Api(transport).ReadMessageAsync("token", Context, "m", TestContext.Current.CancellationToken);
        Assert.Null(result.Failure);
        Assert.True(result.Value!.Truncated);
        Assert.Contains(result.Value.Omissions, omission => omission.Reason == "traversal_limit");
        Assert.True(result.Value.BodyParts.Length <= 256);
    }

    [Fact]
    public async Task TheDecodedBodyLimitPreservesUtf8AndAppliesAcrossMediaTypes()
    {
        var plain = new string('x', 262142);
        var body = JsonSerializer.Serialize(new
        {
            id = "m",
            threadId = "t",
            payload = new
            {
                mimeType = "multipart/alternative",
                parts = new[]
        {
            new { partId = "0", mimeType = "text/plain", body = new { data = Convert.ToBase64String(Encoding.UTF8.GetBytes(plain)).TrimEnd('=') } },
            new { partId = "1", mimeType = "text/html", body = new { data = Convert.ToBase64String(Encoding.UTF8.GetBytes("✓hello")).TrimEnd('=') } },
        }
            }
        });
        using var transport = new Transport(_ => Json(body));
        var result = await Api(transport).ReadMessageAsync("token", Context, "m", TestContext.Current.CancellationToken);
        Assert.Null(result.Failure);
        Assert.Equal(262142, result.Value!.ReturnedBodyBytes);
        Assert.True(result.Value.Truncated);
        Assert.Equal("size_limit", Assert.Single(result.Value.Omissions).Reason);
        Assert.DoesNotContain("�", string.Concat(result.Value.BodyParts.Select(part => part.Content)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALongRetryAfterDoesNotCauseAPrematureRetry()
    {
        using var transport = new Transport(_ =>
        {
            var response = Json("{}", HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(TimeSpan.FromMinutes(1));
            return response;
        });
        var result = await Api(transport).ProfileAsync("token", Context, TestContext.Current.CancellationToken);
        Assert.Equal(GoogleApiFailureKind.RateLimited, result.Failure!.Kind);
        Assert.NotNull(result.Failure.RetryAt);
        Assert.Single(transport.Uris);
    }

    [Fact]
    public async Task CallerCancellationBeforeDraftDispatchMakesNoRequest()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var transport = new Transport(_ => Json("{}"));
        var result = await Api(transport).CreateDraftAsync("token", Context, [], cancelled.Token);
        Assert.Equal(GoogleWriteOutcome.NotDispatched, result.Outcome);
        Assert.True(result.CancellationRequested);
        Assert.Empty(transport.Uris);
    }

    [Fact]
    public async Task AnInternalProviderDeadlineIsAFailureRatherThanCallerCancellation()
    {
        var clock = new FakeTimeProvider();
        using var transport = new BlockingTransport();
        var task = new GmailApi(transport, new GmailModuleOptions(), clock).ProfileAsync("token", Context, TestContext.Current.CancellationToken);
        await transport.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(30));
        var result = await task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(GoogleApiFailureKind.Unavailable, result.Failure!.Kind);
        Assert.Equal(1, transport.Attempts);
    }

    [Fact]
    public async Task CallerCancellationDuringTransportRemainsCancellation()
    {
        using var source = new CancellationTokenSource();
        using var transport = new BlockingTransport();
        var task = new GmailApi(transport, new GmailModuleOptions(), TimeProvider.System).ProfileAsync("token", Context, source.Token);
        await transport.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await source.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(1, transport.Attempts);
    }

    [Fact]
    public async Task CallerCancellationDuringRetryDelayPreventsAnotherAttempt()
    {
        using var source = new CancellationTokenSource();
        using var transport = new Transport(_ => Json("{}", HttpStatusCode.ServiceUnavailable));
        var task = new GmailApi(transport, new GmailModuleOptions(), new FakeTimeProvider()).ProfileAsync("token", Context, source.Token);
        Assert.Single(transport.Uris);
        await source.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Single(transport.Uris);
    }

    [Fact]
    public async Task DraftIdentitySurvivesCallerCancellationWhileReadingItsResponse()
    {
        using var source = new CancellationTokenSource();
        using var transport = new Transport(_ => new(HttpStatusCode.OK) { Content = new CancelOnReadContent(source) });
        var result = await Api(transport).CreateDraftAsync("token", Context, [], source.Token);
        Assert.Equal(GoogleWriteOutcome.Succeeded, result.Outcome);
        Assert.Equal("confirmed", result.DraftId);
        Assert.True(result.CancellationRequested);
        Assert.Single(transport.Uris);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("text/html")]
    public async Task AnExplicitEmptyTextBodyIsDifferentFromAnUnavailableBody(string mediaType)
    {
        var body = JsonSerializer.Serialize(new { id = "m", threadId = "t", payload = new { mimeType = mediaType, body = new { data = "", size = 0 } } });
        using var transport = new Transport(_ => Json(body));
        var result = await Api(transport).ReadMessageAsync("token", Context, "m", TestContext.Current.CancellationToken);
        Assert.Equal("", Assert.Single(result.Value!.BodyParts).Content);
        Assert.Empty(result.Value.Omissions);
    }

    [Theory]
    [InlineData("attached.eml", "message/rfc822", null, "unsupported_body_type")]
    [InlineData("", "message/rfc822", null, "unsupported_body_type")]
    [InlineData("", "multipart/mixed", "external", "attachment_data_not_fetched")]
    public async Task AttachedContentDoesNotBecomeThePrimaryMessageBody(string filename, string mediaType, string? attachmentId, string omission)
    {
        var body = JsonSerializer.Serialize(new
        {
            id = "m",
            threadId = "t",
            payload = new
            {
                mimeType = "multipart/mixed",
                parts = new[]
                {
                    new { partId = "attachment", filename, mimeType = mediaType, body = new { attachmentId },
                        parts = new[] { new { partId = "child", mimeType = "text/plain", body = new { data = "cHJpdmF0ZQ" } } } },
                },
            },
        });
        using var transport = new Transport(_ => Json(body));
        var result = await Api(transport).ReadMessageAsync("token", Context, "m", TestContext.Current.CancellationToken);
        Assert.Empty(result.Value!.BodyParts);
        Assert.True(result.Value.HasAttachments);
        Assert.Equal(omission, Assert.Single(result.Value.Omissions).Reason);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(204)]
    public async Task AWatchStopAcceptsAnEmptySuccessfulResponse(int status)
    {
        using var transport = new Transport(_ => Json("", (HttpStatusCode)status));
        var result = await Api(transport).StopAsync("token", Context, TestContext.Current.CancellationToken);
        Assert.Null(result.Failure);
        Assert.NotNull(result.Value);
        Assert.Single(transport.Uris);
    }

    [Fact]
    public async Task DraftResponseReadingDoesNotRestartTheProviderDeadline()
    {
        var clock = new FakeTimeProvider();
        using var blocked = new BlockingBodyStream();
        using var transport = new Transport(_ =>
        {
            clock.Advance(TimeSpan.FromSeconds(20));
            return new(HttpStatusCode.OK) { Content = new StreamContent(blocked) };
        });
        var task = new GmailApi(transport, new GmailModuleOptions(), clock).CreateDraftAsync("token", Context, [], TestContext.Current.CancellationToken);
        try
        {
            await blocked.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromSeconds(10));
            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
            Assert.Same(task, completed);
            Assert.Equal(GoogleWriteOutcome.Unknown, (await task).Outcome);
        }
        finally
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            await task;
        }
    }

    private static GmailApi Api(Transport transport) => new(transport, new GmailModuleOptions
    {
        GmailEndpoint = new("https://provider.example/gmail/"),
        ProfileEndpoint = new("https://provider.example/profile"),
    }, TimeProvider.System);

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Transport(Func<int, HttpResponseMessage> respond) : HttpMessageHandler, IHttpClientFactory
    {
        public List<Uri> Uris { get; } = [];
        public List<string?> Authorization { get; } = [];
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Uris.Add(request.RequestUri!);
            Authorization.Add(request.Headers.Authorization?.ToString());
            Assert.False(request.Headers.Contains("x-intochatos-funding"));
            return Task.FromResult(respond(Uris.Count));
        }
    }

    private sealed class BlockingTransport : HttpMessageHandler, IHttpClientFactory
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Attempts { get; private set; }
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Json("{}");
        }
    }

    private sealed class CancelOnReadContent(CancellationTokenSource caller) : ByteArrayContent(Encoding.UTF8.GetBytes("{\"id\":\"confirmed\"}"))
    {
        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
        {
            caller.Cancel();
            return base.CreateContentReadStreamAsync(cancellationToken);
        }
    }

    private sealed class BlockingBodyStream : MemoryStream
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}
