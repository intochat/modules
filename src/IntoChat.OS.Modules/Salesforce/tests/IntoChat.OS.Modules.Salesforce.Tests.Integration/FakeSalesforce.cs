using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IntoChat.OS.Modules.Salesforce.Tests.Integration;

// Salesforce on loopback: the OAuth token endpoint and the hosted MCP server the real adapter calls.
// Like Salesforce, neither the code exchange nor a refresh answers with expires_in.
internal sealed class FakeSalesforce : IAsyncDisposable
{
    public const string Org = """{"access_token":"access-token","refresh_token":"refresh-token","instance_url":"https://acme.my.salesforce.com"}""";
    public const string RefusalText = "invalid_grant for refresh-token";

    private static readonly string[] Tools = ["getUserInfo", "getObjectSchema", "soqlQuery", "createRecord", "updateRecord"];
    private readonly WebApplication _app;
    private readonly ConcurrentQueue<string> _requests = new();
    private readonly ConcurrentQueue<string> _refreshTokens = new();

    private FakeSalesforce(WebApplication app) { _app = app; }

    public Uri Origin => new(_app.Urls.Single() + "/");
    public Uri TokenEndpoint => new(Origin, "token");
    public Uri McpEndpoint => new(Origin, "mcp");

    // The body the code exchange answers with.
    public string Grant { get; set; } = Org;
    public bool RefuseRefresh { get; set; }
    // Every MCP request answers 503.
    public bool Down { get; set; }
    // The catalog leaves the write tools out.
    public bool WithoutWriteTools { get; set; }
    public JsonElement Result { get; set; } = JsonSerializer.SerializeToElement(new { });

    public IReadOnlyList<string> Requests => [.. _requests];
    public IReadOnlyList<string> RefreshTokens => [.. _refreshTokens];
    public string? AccessToken { get; private set; }

    public static async Task<FakeSalesforce> StartAsync(CancellationToken ct)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        var fake = new FakeSalesforce(app);
        app.MapPost("/token", (Delegate)fake.Token);
        app.MapPost("/mcp", (Delegate)fake.Mcp);
        app.MapMethods("/mcp", ["GET", "DELETE"], () => Results.StatusCode(StatusCodes.Status405MethodNotAllowed));
        await app.StartAsync(ct);
        return fake;
    }

    private async Task<IResult> Token(HttpContext http)
    {
        var form = await http.Request.ReadFormAsync(http.RequestAborted);
        if (form["grant_type"] == "authorization_code") { return Results.Text(Grant, "application/json"); }
        _refreshTokens.Enqueue(form["refresh_token"].ToString());
        // The refusal carries the token on purpose: the connection must keep it out of its detail.
        return RefuseRefresh
            ? Results.Text(RefusalText, statusCode: StatusCodes.Status400BadRequest)
            : Results.Text("""{"access_token":"fresh-access-token","instance_url":"https://acme.my.salesforce.com"}""", "application/json");
    }

    private async Task<IResult> Mcp(HttpContext http)
    {
        if (Down) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        using var document = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: http.RequestAborted);
        var request = document.RootElement;
        var method = request.GetProperty("method").GetString();
        if (!request.TryGetProperty("id", out var id)) { return Results.Accepted(); }
        AccessToken = http.Request.Headers.Authorization.ToString()["Bearer ".Length..];
        JsonNode result;
        switch (method)
        {
            case "initialize":
                result = new JsonObject
                {
                    ["protocolVersion"] = request.GetProperty("params").GetProperty("protocolVersion").GetString(),
                    ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                    ["serverInfo"] = new JsonObject { ["name"] = "fake-salesforce", ["version"] = "1.0" },
                };
                break;
            case "tools/list":
                _requests.Enqueue("tools/list");
                result = new JsonObject
                {
                    ["tools"] = new JsonArray([.. Tools
                        .Where(tool => !WithoutWriteTools || tool is not ("createRecord" or "updateRecord"))
                        .Select(tool => (JsonNode)new JsonObject { ["name"] = tool, ["inputSchema"] = new JsonObject { ["type"] = "object" } })]),
                };
                break;
            case "tools/call":
                _requests.Enqueue(request.GetProperty("params").GetProperty("name").GetString()!);
                result = new JsonObject
                {
                    ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = Result.GetRawText() }),
                    ["structuredContent"] = JsonNode.Parse(Result.GetRawText()),
                    ["isError"] = false,
                };
                break;
            default:
                return Reply(id, "error", new JsonObject { ["code"] = -32601, ["message"] = "Method not found" });
        }
        return Reply(id, "result", result);
    }

    private static IResult Reply(JsonElement id, string kind, JsonNode body)
        => Results.Text(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = JsonNode.Parse(id.GetRawText()), [kind] = body }.ToJsonString(), "application/json");

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

// The silo's wall clock moved forward by Shift.
internal sealed class ShiftedClock : TimeProvider
{
    public TimeSpan Shift { get; set; }

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + Shift;
}
