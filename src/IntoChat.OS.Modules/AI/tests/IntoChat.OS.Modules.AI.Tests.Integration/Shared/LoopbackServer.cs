using System.Net;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IntoChat.OS.Modules.AI.Tests.Integration;

internal sealed record CapturedRequest(string Method, string? Path, string Body, string? Authorization);

internal sealed class LoopbackServer : IDisposable
{
    private readonly WebApplication app;
    private readonly Channel<PendingRequest> requests = Channel.CreateUnbounded<PendingRequest>();
    private readonly CancellationTokenSource shutdown = new();

    public LoopbackServer()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        // Kestrel owns the socket from allocation through disposal. Probing a
        // free port and handing it to HttpListener leaves a parallel-start race.
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        app = builder.Build();
        app.Run(async context =>
        {
            using var stopped = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, shutdown.Token);
            var pending = new PendingRequest(context);
            try
            {
                await requests.Writer.WriteAsync(pending, stopped.Token);
                await pending.Completed.Task.WaitAsync(stopped.Token);
            }
            catch (OperationCanceledException) when (stopped.IsCancellationRequested) { context.Abort(); }
        });
        try
        {
            app.StartAsync().GetAwaiter().GetResult();
            Url = app.Urls.Single().TrimEnd('/') + "/";
        }
        catch
        {
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            shutdown.Dispose();
            throw;
        }
    }

    public string Url { get; }

    public Task<CapturedRequest> ReplyOnce(string mediaType, byte[] response, CancellationToken cancellationToken, HttpStatusCode status = HttpStatusCode.OK)
        => Reply(mediaType, _ => response, cancellationToken, status);

    // The next request, answered from its body: a provider whose answer depends on what it was asked.
    public Task<CapturedRequest> ReplyOnce(string mediaType, Func<string, string> respond, CancellationToken cancellationToken)
        => Reply(mediaType, body => Encoding.UTF8.GetBytes(respond(body)), cancellationToken, HttpStatusCode.OK);

    private async Task<CapturedRequest> Reply(string mediaType, Func<string, byte[]> respond, CancellationToken cancellationToken, HttpStatusCode status)
    {
        var pending = await requests.Reader.ReadAsync(cancellationToken);
        var context = pending.Context;
        try
        {
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var captured = new CapturedRequest(context.Request.Method, context.Request.Path.Value,
                await reader.ReadToEndAsync(cancellationToken), context.Request.Headers.Authorization.FirstOrDefault());
            var response = respond(captured.Body);
            context.Response.StatusCode = (int)status;
            context.Response.ContentType = mediaType;
            context.Response.ContentLength = response.Length;
            await context.Response.Body.WriteAsync(response, cancellationToken);
            return captured;
        }
        catch { context.Abort(); throw; }
        finally { pending.Completed.TrySetResult(); }
    }

    // The next request, received and never answered: a provider that is still working on it.
    public async Task Hold(CancellationToken cancellationToken)
    { _ = await requests.Reader.ReadAsync(cancellationToken); }

    public Task<CapturedRequest> ReplyOnce(string mediaType, string response, CancellationToken cancellationToken, HttpStatusCode status = HttpStatusCode.OK)
        => ReplyOnce(mediaType, Encoding.UTF8.GetBytes(response), cancellationToken, status);

    public void Dispose()
    {
        shutdown.Cancel();
        requests.Writer.TryComplete();
        try { app.StopAsync().GetAwaiter().GetResult(); }
        finally
        {
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            shutdown.Dispose();
        }
    }

    private sealed record PendingRequest(HttpContext Context)
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
