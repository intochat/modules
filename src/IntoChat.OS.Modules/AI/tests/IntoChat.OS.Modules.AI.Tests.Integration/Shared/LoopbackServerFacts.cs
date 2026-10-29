using System.Net;
using System.Collections.Concurrent;

namespace IntoChat.OS.Modules.AI.Tests.Integration;

public sealed class LoopbackServerFacts
{
    [Fact]
    public async Task ConcurrentServersOwnDistinctLiveEndpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        var owned = new ConcurrentBag<LoopbackServer>();
        try
        {
            var servers = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() =>
            {
                var server = new LoopbackServer();
                owned.Add(server);
                return server;
            }, ct)));
            Assert.Equal(servers.Length, servers.Select(server => server.Url).Distinct().Count());
            using var client = new HttpClient();
            await Task.WhenAll(servers.Select(async (server, index) =>
            {
                var received = server.ReplyOnce("text/plain", "answer-" + index, ct, HttpStatusCode.Accepted);
                using var request = new HttpRequestMessage(HttpMethod.Post, server.Url + "probe")
                {
                    Content = new StringContent("body-" + index),
                };
                request.Headers.Authorization = new("Bearer", "token-" + index);
                using var response = await client.SendAsync(request, ct);
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                Assert.Equal("answer-" + index, await response.Content.ReadAsStringAsync(ct));
                Assert.Equal(new CapturedRequest("POST", "/probe", "body-" + index, "Bearer token-" + index), await received);
            }));
        }
        finally { foreach (var server in owned) { server.Dispose(); } }
    }

    [Fact]
    public async Task HoldReceivesWithoutAnsweringAndDisposalReleasesTheClient()
    {
        var ct = TestContext.Current.CancellationToken;
        var server = new LoopbackServer();
        using var client = new HttpClient();
        var received = server.Hold(ct);
        var response = client.GetAsync(server.Url, ct);
        try
        {
            await received;
            Assert.False(response.IsCompleted);
        }
        finally { server.Dispose(); }
        await Assert.ThrowsAsync<HttpRequestException>(() => response);
    }
}
