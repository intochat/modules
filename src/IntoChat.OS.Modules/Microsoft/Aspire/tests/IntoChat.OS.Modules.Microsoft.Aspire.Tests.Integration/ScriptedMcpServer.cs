using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace IntoChat.OS.Modules.Microsoft.Aspire.Tests;

// The candidate's MCP as a script: initialize and tools/list answer statically, each tools/call is
// answered from the table by tool name plus contract/key/method, in the SSE shape the real server uses.
internal sealed class ScriptedMcpServer : IAsyncDisposable
{
    private static readonly string[] Tools = ["neurons_context", "neurons_list", "neurons_get", "neurons_call"];
    private readonly WebApplication _app;
    public Dictionary<string, string> Answers { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Failures { get; } = [];
    public List<string> Calls { get; } = [];
    public string Endpoint => _app.Urls.Single();

    private ScriptedMcpServer(WebApplication app) => _app = app;

    public static async Task<ScriptedMcpServer> StartAsync(CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        var server = new ScriptedMcpServer(app);
        app.MapPost("/brains/{brain}/mcp", async (string brain, HttpContext context) =>
        {
            using var request = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            var root = request.RootElement;
            if (!root.TryGetProperty("id", out var id)) { context.Response.StatusCode = StatusCodes.Status202Accepted; return; }
            var method = root.GetProperty("method").GetString();
            if (method is not ("initialize" or "tools/list" or "tools/call"))
            {
                context.Response.ContentType = "text/event-stream";
                await context.Response.WriteAsync("event: message\ndata: " + JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code = -32601, message = "method not found" } }) + "\n\n", context.RequestAborted);
                return;
            }
            object result = method switch
            {
                "initialize" => new { protocolVersion = root.GetProperty("params").GetProperty("protocolVersion").GetString(), capabilities = new { tools = new { } }, serverInfo = new { name = "scripted", version = "1" } },
                "tools/list" => new { tools = Tools.Select(name => new { name, description = name, inputSchema = new { type = "object" } }) },
                "tools/call" => server.Call(brain, root.GetProperty("params")),
                _ => throw new InvalidOperationException(method),
            };
            context.Response.ContentType = "text/event-stream";
            await context.Response.WriteAsync("event: message\ndata: " + JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result }) + "\n\n", context.RequestAborted);
        });
        app.MapDelete("/brains/{brain}/mcp", () => Results.Ok());
        await app.StartAsync(ct);
        return server;
    }

    private object Call(string brain, JsonElement parameters)
    {
        var tool = parameters.GetProperty("name").GetString()!;
        var arguments = parameters.GetProperty("arguments");
        var key = tool switch
        {
            "neurons_context" => "neurons_context",
            "neurons_list" => $"neurons_list {arguments.GetProperty("contract").GetString()}",
            "neurons_get" => $"neurons_get {arguments.GetProperty("contract").GetString()} {arguments.GetProperty("key").GetString()}",
            _ => $"neurons_call {arguments.GetProperty("contract").GetString()} {arguments.GetProperty("key").GetString()} {arguments.GetProperty("method").GetString()}",
        };
        Calls.Add(key);
        // As the real tool: neurons_call takes its arguments as one JSON array string, and anything else is an invocation error.
        if (tool == "neurons_call" && arguments.GetProperty("arguments").ValueKind != JsonValueKind.String)
        { return new { content = new[] { new { type = "text", text = "An error occurred invoking 'neurons_call'." } }, isError = true }; }
        var text = Answers.TryGetValue(key, out var answer) ? answer : tool switch
        {
            "neurons_context" => $"{{\"principalId\":\"owner\",\"brainId\":\"{brain}\"}}",
            "neurons_list" => "[]",
            _ when key.StartsWith("neurons_call IntoChat.OS.Modules.Apps.IFeatureReconciler ", StringComparison.Ordinal) => "",
            _ => null,
        };
        return text is null || Failures.Contains(key)
            ? new { content = new[] { new { type = "text", text = $"no script for {key}" } }, isError = true }
            : new { content = new[] { new { type = "text", text } }, isError = false };
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
