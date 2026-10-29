using IntoChat.OS.Modules.Microsoft.Aspire;
using System.Text.Json;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Modules.Microsoft.Aspire.Candidates;
using IntoChat.OS.ServiceDefaults;

namespace IntoChat.OS.Modules.Microsoft.Aspire.Deployment;

// The live AppHost through the `aspire` CLI (13.6 has no `aspire mcp start`): `describe`, `resource restart`, `otel spans`.
// The AppHost project is found from the repository, Workdir's parent.
internal sealed class AspireCliClusterHost : IClusterHost
{
    public const string SiloResource = "OperatingSystem";
    public const string McpResource = "intochat-os-mcp";
    // `otel spans` caps its result; a verification run's spans must not fall off that window.
    private const string SpanLimit = "10000";
    private const string AppHostProject = "src/IntoChat.OS.AppHost/IntoChat.OS.AppHost.csproj";

    private readonly IProcessRunner processes;
    private readonly Func<CancellationToken, Task<string>> resolveWorkdir;
    private Task<string>? workdirRead;
    private readonly string? self;
    private readonly Func<CancellationToken, Task<IReadOnlyDictionary<string, Uri>>> resolveFronts;
    private Task<IReadOnlyDictionary<string, Uri>>? frontsRead;
    private readonly HttpClient http;

    // Aspire names each instance's telemetry after its resource: OTEL_SERVICE_NAME is this silo's instance name.
    public AspireCliClusterHost(IModuleIntegrations<AspireModule> integrations, IProcessRunner processes)
        : this(processes, ct => Workdir(integrations, ct), Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME"), ct => Fronts(integrations, ct))
    {
    }

    internal AspireCliClusterHost(IProcessRunner processes, Func<CancellationToken, Task<string>> workdir, string? self = null,
        Func<CancellationToken, Task<IReadOnlyDictionary<string, Uri>>>? fronts = null, HttpMessageHandler? handler = null)
    {
        this.processes = processes;
        resolveWorkdir = workdir;
        this.self = self;
        resolveFronts = fronts ?? (_ => Task.FromResult<IReadOnlyDictionary<string, Uri>>(new Dictionary<string, Uri>()));
        http = handler is null ? SharedHttp : new HttpClient(handler) { Timeout = ProbeTimeout };
    }

    // Read once, like Workdir: the fronts are the AppHost's fixed ports and never move while it runs.
    private async Task<Uri> FrontOf(string instance, CancellationToken ct)
    {
        var read = frontsRead ??= resolveFronts(CancellationToken.None);
        IReadOnlyDictionary<string, Uri> fronts;
        try { fronts = await read.WaitAsync(ct); }
        catch when (read.IsFaulted || read.IsCanceled) { frontsRead = null; throw; }
        var role = RoleOf(instance);
        return fronts.TryGetValue(role, out var front) ? front : throw new InvalidOperationException($"the aspire Integration names no front for {role}");
    }

    internal static string RoleOf(string instance) => instance[..^2];

    // The front's own route to one instance (AspireModuleHosting.InstancePath): 200 only while the front's health
    // check counts the instance in and reaches it over the same tunnel it routes requests through.
    internal static string InstancePath(string instance) => $"/_instances/{instance}{HealthProbes.ReadyPath}";

    // The deployment's Workdir is read from its Integration once: a roll must not call a grain while a silo leaves
    // the cluster (seen live: the Integration's directory lookup hit the stopping silo and failed the step).
    private async Task<string> DeploymentWorkdir(CancellationToken ct)
    {
        var read = workdirRead ??= resolveWorkdir(CancellationToken.None);
        try { return await read.WaitAsync(ct); }
        catch when (read.IsFaulted || read.IsCanceled) { workdirRead = null; throw; }
    }

    public async Task<string> CurrentSha(CancellationToken ct) => LiveBuildFile.Read(await DeploymentWorkdir(ct))?.Sha ?? string.Empty;

    public async Task SetLive(string sha, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha);
        LiveBuildFile.Write(await DeploymentWorkdir(ct), sha);
    }

    // The MCP servers are Orleans clients of the same build: they restart onto live.json's build first, then the
    // silos. This call runs inside one silo, so that silo restarts last: its restart ends the caller. Stopping
    // the silo signals its console, and a CLI sharing that console cancels the restart between stop and start
    // (seen live): the caller's own restart is issued detached and is not awaited.
    public async Task RestartSilos(CancellationToken ct)
    {
        var names = (await Describe(ct)).Select(resource => Text(resource, "displayName", "name")).OfType<string>().Distinct().Order(StringComparer.Ordinal).ToArray();
        foreach (var mcp in names.Where(name => Instance(name, McpResource)))
        {
            await Aspire(["resource", mcp, "restart"], ct);
        }
        var silos = names.Where(name => Instance(name, SiloResource)).ToArray();
        var own = self is not null && silos.Contains(self) ? self : null;
        foreach (var silo in silos.Where(name => name != own))
        {
            await Aspire(["resource", silo, "restart"], ct);
        }
        if (own is not null)
        {
            var repository = await Repository(ct);
            processes.Detach("aspire", Arguments(repository, ["resource", own, "restart"]), repository);
        }
    }

    public string? Self => self;

    public async Task<string[]> Instances(CancellationToken ct)
    {
        var names = (await Describe(ct)).Select(resource => Text(resource, "displayName", "name")).OfType<string>().Distinct().Order(StringComparer.Ordinal).ToArray();
        return [.. names.Where(name => Instance(name, McpResource)), .. names.Where(name => Instance(name, SiloResource))];
    }

    // Restarting the silo this call runs in ends the caller: that restart is detached, as in RestartSilos.
    public async Task RestartInstance(string name, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name == self)
        {
            var repository = await Repository(ct);
            processes.Detach("aspire", Arguments(repository, ["resource", name, "restart"]), repository);
            return;
        }
        await Aspire(["resource", name, "restart"], ct);
    }

    public async Task DrainInstance(string name, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var resource = (await Describe(ct)).FirstOrDefault(item => Text(item, "displayName", "name") == name);
        var endpoint = (resource.ValueKind == JsonValueKind.Object ? Endpoint(resource) : null)
            ?? throw new InvalidOperationException($"instance {name} has no http endpoint to drain");
        using var response = await http.PostAsync(new Uri(endpoint, HealthProbes.DrainPath), null, ct);
        response.EnsureSuccessStatusCode();
    }

    // The instance has left the front once the front's route to it stops answering 200; before it stops, another
    // instance of the role must still answer through the front, or stopping this one takes the port down.
    public async Task WaitOutOfFront(string name, TimeSpan budget, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var front = await FrontOf(name, ct);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bounded.CancelAfter(budget);
        try
        {
            while (await Routed(front, name, bounded.Token)) { await Task.Delay(ReadyInterval, bounded.Token); }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new InstanceNeverReadyException($"the front still routed {name} after {budget}", new TimeoutException());
        }
        var role = RoleOf(name);
        foreach (var peer in await Instances(ct))
        {
            if (peer != name && RoleOf(peer) == role && await Routed(front, peer, ct)) { return; }
        }
        throw new InstanceNeverReadyException($"no other {role} instance is routable through the front; {name} must not stop");
    }

    private Task<bool> Routed(Uri front, string instance, CancellationToken ct) => Ready(new Uri(front, InstancePath(instance)), ct);

    // A readiness wait on a freshly started process: Running in `describe`, then the front's route to it answers 200
    // (its /ready through the front's tunnel, counted in by the front's health check). The budget bounds it; an
    // instance that stops fails at once.
    public async Task WaitInstance(string name, DateTimeOffset since, TimeSpan budget, CancellationToken ct)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bounded.CancelAfter(budget);
        try
        {
            while (true)
            {
                var resource = (await Describe(bounded.Token)).FirstOrDefault(item => Text(item, "displayName", "name") == name);
                var state = resource.ValueKind == JsonValueKind.Object ? Text(resource, "state") : null;
                // The old process stopping (restart issued, not yet started again) is not a failed start: only a
                // process that started after the restart was issued and then stopped is.
                if (state is "Exited" or "Finished" or "FailedToStart" && StartedSince(resource, since))
                {
                    throw new InstanceNeverReadyException($"instance {name} stopped ({state}) while starting");
                }
                if (state == "Running" && await Routed(await FrontOf(name, bounded.Token), name, bounded.Token))
                {
                    return;
                }
                await Task.Delay(ReadyInterval, bounded.Token);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new InstanceNeverReadyException($"instance {name} was not ready within {budget}", new TimeoutException());
        }
    }

    internal static bool StartedSince(JsonElement resource, DateTimeOffset since)
        => resource.ValueKind != JsonValueKind.Object || Text(resource, "startTimestamp") is not { } started
            || !DateTimeOffset.TryParse(started, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var start)
            || start >= since;

    private static readonly TimeSpan ReadyInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);
    private static readonly HttpClient SharedHttp = new() { Timeout = ProbeTimeout };

    private async Task<bool> Ready(Uri probe, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(probe, ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return false; }
    }

    // `describe` lists a resource's urls as strings or as objects carrying `url`; the instance's own http endpoint.
    internal static Uri? Endpoint(JsonElement resource)
    {
        if (!resource.TryGetProperty("urls", out var urls) || urls.ValueKind != JsonValueKind.Array) { return null; }
        return urls.EnumerateArray()
            .Select(url => url.ValueKind == JsonValueKind.String ? url.GetString() : url.ValueKind == JsonValueKind.Object ? Text(url, "url") : null)
            .Select(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null)
            .OfType<Uri>()
            .OrderBy(uri => uri.Scheme == Uri.UriSchemeHttp ? 0 : 1)
            .FirstOrDefault(uri => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    // The AppHost runs each role as named instances {role}-{x}, x a single letter (AspireModuleHosting.Instances);
    // {role}-front and the rebuilders are not instances.
    internal static bool Instance(string? name, string role)
        => name is not null && name.Length == role.Length + 2 && name.StartsWith(role + "-", StringComparison.Ordinal) && char.IsAsciiLetterLower(name[^1]);

    private async Task<JsonElement[]> Describe(CancellationToken ct)
    {
        using var json = JsonDocument.Parse(JsonPart(await Aspire(["describe", "--format", "Json"], ct)));
        return [.. Resources(json.RootElement).Select(resource => resource.Clone())];
    }

    // `otel spans` lists one JSON object per span; `source` is the exporting service's name (its OTEL service.name).
    public async Task<int> Spans(string service, string span, CancellationToken ct)
    {
        using var json = JsonDocument.Parse(JsonPart(await Aspire(["otel", "spans", "--format", "Json", "--search", span, "--limit", SpanLimit], ct)));
        return json.RootElement.ValueKind != JsonValueKind.Array ? 0
            : json.RootElement.EnumerateArray().Count(item => Text(item, "name") == span && From(Text(item, "source"), service));
    }

    // A replica's service name carries a suffix: candidate-{sha}-mcp is still candidate-{sha}'s.
    private static bool From(string? source, string service)
        => source is not null && (source == service || source.StartsWith(service + "-", StringComparison.Ordinal));

    internal static IEnumerable<JsonElement> Resources(JsonElement root) => root.ValueKind switch
    {
        JsonValueKind.Array => root.EnumerateArray(),
        JsonValueKind.Object when root.TryGetProperty("resources", out var resources) && resources.ValueKind == JsonValueKind.Array => resources.EnumerateArray(),
        _ => [],
    };

    private static string? Text(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String) { return value.GetString(); }
        }
        return null;
    }

    // The CLI may print log lines before its JSON: the document starts at the first line that opens one.
    internal static string JsonPart(string output)
    {
        var lines = output.Split('\n');
        var start = Array.FindIndex(lines, line => line.TrimStart().StartsWith('[') || line.TrimStart().StartsWith('{'));
        return start < 0
            ? throw new InvalidOperationException($"aspire printed no JSON: {output}")
            : string.Join('\n', lines[start..]);
    }

    private async Task<string> Aspire(string[] command, CancellationToken ct)
    {
        var repository = await Repository(ct);
        var result = await processes.RunAsync("aspire", Arguments(repository, command), repository, null, ct);
        return result.ExitCode == 0
            ? result.Output
            : throw new InvalidOperationException($"aspire {string.Join(' ', command)} exited {result.ExitCode}: {result.Error}{result.Output}");
    }

    private async Task<string> Repository(CancellationToken ct) => Path.GetDirectoryName(Path.GetFullPath(await DeploymentWorkdir(ct)))!;

    private static string[] Arguments(string repository, string[] command)
        => [.. command, "--apphost", Path.Combine(repository, AppHostProject), "--non-interactive", "--nologo"];

    private static async Task<IReadOnlyDictionary<string, Uri>> Fronts(IModuleIntegrations<AspireModule> integrations, CancellationToken ct)
    {
        var snapshot = await integrations.Get("aspire").ReadAsync(ct);
        return snapshot.Settings.TryGetValue(AspireModule.FrontsField, out var fronts) && fronts is { Length: > 0 }
            ? ParseFronts(fronts)
            : throw new InvalidOperationException("the aspire Integration has no Fronts");
    }

    // {"OperatingSystem":"http://localhost:5080", ...}: each role's front, as the AppHost published it.
    internal static IReadOnlyDictionary<string, Uri> ParseFronts(string json)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(json)!.ToDictionary(pair => pair.Key, pair => new Uri(pair.Value), StringComparer.Ordinal);

    private static async Task<string> Workdir(IModuleIntegrations<AspireModule> integrations, CancellationToken ct)
    {
        var snapshot = await integrations.Get("aspire").ReadAsync(ct);
        return snapshot.Settings.TryGetValue("Workdir", out var workdir) && workdir is { Length: > 0 }
            ? Path.GetFullPath(workdir)
            : throw new InvalidOperationException("the aspire Integration has no Workdir");
    }
}
