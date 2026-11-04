using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DigitalBrain;
using DigitalBrain.Rules;
using DigitalBrain.Spi;
using DigitalBrain.Testing;
using IntoChat.OS.Modules.Tools;

namespace IntoChat.OS.Modules.Mcp.Tests.Unit;

public sealed class McpPolicyFacts
{
    private static readonly NeuronId PoliciesId = NeuronId.Parse(IPolicies.Key("b"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task Adopt(TestOS os, PolicyRule rule)
    {
        var policies = os.Get<IPolicies>(PoliciesId);
        var outcome = await policies.Propose(rule, Ct);
        Assert.Equal(ProposalStatus.Pending, outcome.Status);
        await os.Confirmations.Approve(outcome.PlanId!);
        await os.Idle();
    }

    private static async Task<(TestOS Os, McpServerNeuron Neuron)> CreateHarnessAsync(Uri endpoint)
    {
        var os = TestOS.Local().Register(typeof(McpModule).Assembly);
        var id = new NeuronId(new BrainId("b"), "mcp.server", "mcp/s1");
        var context = new FakeNeuronContext(id);
        context.Proxies.Register(PoliciesId, os.Get<IPolicies>(PoliciesId));
        var tool = new ToolDescriptor("echo", "Echo", "", "{\"type\":\"object\"}", false);
        await context.MemoryState.Save(
            new McpServerStore
            {
                State = new McpServerState("Fixture", endpoint.AbsoluteUri, true, [tool], null, DateTimeOffset.UtcNow),
            },
            []);

        var neuron = new McpServerNeuron();
        NeuronAttachment.Attach(neuron, context);
        await NeuronAttachment.Activate(neuron, CancellationToken.None);
        return (os, neuron);
    }

    [Fact]
    public async Task ARefusingPolicyStopsTheToolRunBeforeTheApiCall()
    {
        await using var server = FakeMcpEndpoint.Start();
        var (os, neuron) = await CreateHarnessAsync(server.Endpoint);
        await using var _ = os;
        var rule = new PolicyRule(
            "r1",
            "mcp.tool.run",
            PolicyMeasure.Count,
            new PolicyWindow(PolicyWindowKind.None, null),
            0m,
            null,
            "Never run an MCP tool",
            os.Clock.GetUtcNow());
        await Adopt(os, rule);

        await Assert.ThrowsAsync<RuleViolationException>(() => neuron.Run("echo", "{}", Ct));
        Assert.Equal(0, server.ToolCalls);
    }

    [Fact]
    public async Task AnAllowedToolRunIsCountedOnce()
    {
        await using var server = FakeMcpEndpoint.Start();
        var (os, neuron) = await CreateHarnessAsync(server.Endpoint);
        await using var _ = os;
        var rule = new PolicyRule(
            "r1",
            "mcp.tool.run",
            PolicyMeasure.Count,
            new PolicyWindow(PolicyWindowKind.Day, null),
            1m,
            null,
            "Allow at most 1 count of run an MCP tool per day",
            os.Clock.GetUtcNow());
        await Adopt(os, rule);

        var first = await neuron.Run("echo", "{}", Ct);
        Assert.False(first.IsError);
        Assert.Equal("ok", first.Text);
        Assert.Equal(1, server.ToolCalls);

        await Assert.ThrowsAsync<RuleViolationException>(() => neuron.Run("echo", "{}", Ct));
        Assert.Equal(1, server.ToolCalls);
    }

    [Fact]
    public async Task AFailedApiCallReleasesTheReservation()
    {
        await using var server = FakeMcpEndpoint.Start();
        var (os, neuron) = await CreateHarnessAsync(server.Endpoint);
        await using var _ = os;
        var rule = new PolicyRule(
            "r1",
            "mcp.tool.run",
            PolicyMeasure.Count,
            new PolicyWindow(PolicyWindowKind.Day, null),
            1m,
            null,
            "Allow at most 1 count of run an MCP tool per day",
            os.Clock.GetUtcNow());
        await Adopt(os, rule);

        server.FailNextToolCall = true;
        await Assert.ThrowsAnyAsync<Exception>(() => neuron.Run("echo", "{}", Ct));

        var second = await neuron.Run("echo", "{}", Ct);
        Assert.False(second.IsError);
        Assert.Equal("ok", second.Text);

        await Assert.ThrowsAsync<RuleViolationException>(() => neuron.Run("echo", "{}", Ct));
    }

    [Fact]
    public void TheManifestDeclaresTheAction()
    {
        var action = Assert.Single(ModuleManifest.Of(typeof(McpModule)).Actions, a => a.Name == "mcp.tool.run");
        Assert.Equal("run an MCP tool", action.Description);
        Assert.Null(action.AmountUnit);
    }

    private sealed class FakeMcpEndpoint : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loop;
        private int _toolCalls;

        private FakeMcpEndpoint(int port)
        {
            var prefix = $"http://127.0.0.1:{port}/mcp/";
            _listener.Prefixes.Add(prefix);
            _listener.Start();
            Endpoint = new Uri(prefix);
            _loop = Task.Run(LoopAsync);
        }

        public int ToolCalls => Volatile.Read(ref _toolCalls);
        public bool FailNextToolCall { get; set; }
        public Uri Endpoint { get; }

        public static FakeMcpEndpoint Start()
        {
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return new FakeMcpEndpoint(port);
        }

        private async Task LoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch { break; }
                _ = Task.Run(() => HandleAsync(context));
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            using var response = context.Response;
            if (!string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = 405;
                return;
            }

            using var document = await JsonDocument.ParseAsync(context.Request.InputStream);
            var request = document.RootElement;
            var method = request.GetProperty("method").GetString();
            if (!request.TryGetProperty("id", out var id))
            {
                response.StatusCode = 202;
                return;
            }

            string json;
            if (method == "initialize")
            {
                var init = new JsonObject
                {
                    ["protocolVersion"] = request.GetProperty("params").GetProperty("protocolVersion").GetString(),
                    ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                    ["serverInfo"] = new JsonObject { ["name"] = "fake-mcp", ["version"] = "1.0" },
                };
                json = Reply(id, "result", init);
            }
            else if (method == "tools/call")
            {
                Interlocked.Increment(ref _toolCalls);
                if (FailNextToolCall)
                {
                    FailNextToolCall = false;
                    json = Reply(id, "error", new JsonObject { ["code"] = -32603, ["message"] = "Simulated tool failure" });
                }
                else
                {
                    var result = new JsonObject
                    {
                        ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = "ok" }),
                        ["isError"] = false,
                    };
                    json = Reply(id, "result", result);
                }
            }
            else
            {
                json = Reply(id, "error", new JsonObject { ["code"] = -32601, ["message"] = "Method not found" });
            }

            var bytes = Encoding.UTF8.GetBytes(json);
            response.StatusCode = 200;
            response.ContentType = "application/json";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes);
        }

        private static string Reply(JsonElement id, string kind, JsonNode body)
            => new JsonObject { ["jsonrpc"] = "2.0", ["id"] = JsonNode.Parse(id.GetRawText()), [kind] = body }.ToJsonString();

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            _listener.Stop();
            _listener.Close();
            try { await _loop; } catch { }
            _cts.Dispose();
        }
    }
}
