using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Connections.Configuration;
using IntoChat.OS.Framework.Configuration;
using IntoChat.OS.Framework.Modules.Configuration;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Connections.Identity.Configuration;
using Microsoft.Extensions.Configuration;

namespace IntoChat.OS.Modules.Microsoft.Aspire.Candidates;

// Candidates are git worktrees under {Workdir}/worktrees, published to {Workdir}/builds/{sha}/{os,mcp}
// and run on a private network beside their own Azurite, Postgres and ClickHouse and their own
// intochat-os-mcp. The repository is Workdir's parent.
internal sealed partial class DockerCandidateHost : ICandidateHost
{
    public const int FirstPort = 5190;
    private const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:3.35.0";
    private const string PostgresImage = "postgres:18.3";
    private const string ClickHouseImage = "clickhouse/clickhouse-server:25.8";
    private const string OsProject = "src/IntoChat.OS.Kernel/IntoChat.OS.Kernel/IntoChat.OS.Kernel.csproj";
    private const string McpProject = "src/IntoChat.OS.Mcp/IntoChat.OS.Mcp/IntoChat.OS.Mcp.csproj";
    private const string PostgresDatabase = "intochat_os";
    private static readonly TimeSpan DependencyBudget = TimeSpan.FromSeconds(90);

    // Deployment shape the OS reads besides its Integrations: module options and identity. No Integration
    // travels from configuration; the model's arrive through the aspire Integration (CandidateSecrets).
    private static readonly string[] CopiedSections = [IntoChatOSConfiguration.ModulesSectionName + ":", IdentityHostOptions.SectionName + ":"];

    private readonly IProcessRunner processes;
    private readonly IConfiguration live;
    private readonly Func<CancellationToken, Task<string>> workdir;
    private readonly Func<CancellationToken, Task<CandidateSecrets>> secrets;
    private readonly Func<int, bool> listening;
    private readonly Func<string, int, CancellationToken, Task> dependenciesReady;
    private readonly HttpClient http;
    private readonly Dictionary<string, int> ports = [];
    private readonly Lock gate = new();

    public DockerCandidateHost(IModuleIntegrations<AspireModule> integrations, IConfiguration configuration, IProcessRunner processes)
        : this(processes, configuration, ct => Workdir(integrations, ct), ct => Secrets(integrations, ct), null, new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
    {
    }

    internal DockerCandidateHost(
        IProcessRunner processes,
        IConfiguration configuration,
        Func<CancellationToken, Task<string>> workdir,
        Func<CancellationToken, Task<CandidateSecrets>> secrets,
        Func<int, bool>? listening,
        HttpClient http,
        Func<string, int, CancellationToken, Task>? dependenciesReady = null)
    {
        this.processes = processes;
        live = configuration;
        this.workdir = workdir;
        this.secrets = secrets;
        this.listening = listening ?? Listening;
        this.dependenciesReady = dependenciesReady ?? DependenciesReady;
        this.http = http;
    }

    public IReadOnlyDictionary<string, string> LastRunEnvironment { get; private set; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> LastMcpEnvironment { get; private set; } = new Dictionary<string, string>();

    public async Task<PreparedCandidate> Prepare(string baseSha, string summary, IReadOnlyDictionary<string, string> files, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(files);
        var root = await workdir(ct);
        var git = Git(root);
        var staging = Path.Combine(root, "worktrees", $"staging-{Guid.NewGuid():N}");
        var added = false;
        try
        {
            await git.AddWorktree(staging, baseSha, ct);
            added = true;
            foreach (var (path, content) in files)
            {
                var target = Path.GetFullPath(Path.Combine(staging, path));
                if (!target.StartsWith(Path.GetFullPath(staging) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                { throw new CandidateHostException("prepare", $"{path} leaves the worktree"); }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllTextAsync(target, content, ct);
            }
            await git.Stage(staging, ct);
            await git.Commit(staging, summary, ct);
            var sha = await git.ShortSha(staging, ct);
            var worktree = WorktreeOf(root, sha);
            await git.MoveWorktree(staging, worktree, ct);
            return new(sha, worktree);
        }
        catch (Exception error)
        {
            if (added) { await git.RemoveWorktree(staging, CancellationToken.None); }
            if (error is CandidateHostException or OperationCanceledException) { throw; }
            throw new CandidateHostException("prepare", error.Message);
        }
    }

    // The candidate is one commit over its base: every project a changed file sits in builds, and
    // every test project of a module it touches runs.
    public async Task BuildGate(string sha, CancellationToken ct)
    {
        Checked(sha);
        var root = await workdir(ct);
        var worktree = WorktreeOf(root, sha);
        string[] changed;
        try { changed = await Git(root).ChangedFiles(worktree, "HEAD~1", ct); }
        catch (InvalidOperationException error) { throw new CandidateHostException("gate", error.Message); }
        var projects = changed.Select(file => ProjectOf(worktree, file)).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var project in projects)
        { await Dotnet("gate", worktree, ct, "build", project); }
        var tests = projects
            .Select(project => Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(project)!)!, "tests"))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.GetDirectories(folder).Where(dir => Directory.EnumerateFiles(dir, "*.csproj").Any()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var test in tests)
        { await Dotnet("gate", worktree, ct, "test", "--project", test); }
    }

    // The silo and its MCP front door are two images, each published into its own folder.
    public async Task<string> Publish(string sha, CancellationToken ct)
    {
        Checked(sha);
        var root = await workdir(ct);
        var worktree = WorktreeOf(root, sha);
        var build = Path.Combine(root, "builds", sha);
        foreach (var (folder, project, dll, image) in new[]
        {
            ("os", OsProject, "IntoChat.OS.Kernel.dll", ImageOf(sha)),
            ("mcp", McpProject, "IntoChat.OS.Mcp.dll", McpImageOf(sha)),
        })
        {
            var output = Path.Combine(build, folder);
            var dockerfile = Path.Combine(root, $"Candidate.{folder}.Dockerfile");
            await Dotnet("publish", worktree, ct, "publish", project, "-c", "Release", "-o", output);
            // Infer a Linux image with this candidate's SDK, leaving the published deployment files portable.
            var baseImage = (await Dotnet("publish", worktree, ct, "msbuild", project, "-t:ComputeContainerBaseImage",
                "-p:Configuration=Release", "-p:EnableSdkContainerSupport=true", "-p:RuntimeIdentifier=linux-x64", "-getProperty:ContainerBaseImage")).Trim();
            if (!ImageReference().IsMatch(baseImage)) { throw new CandidateHostException("publish", $"{project} returned an invalid ContainerBaseImage"); }
            await File.WriteAllTextAsync(dockerfile, $"FROM {baseImage}\nWORKDIR /app\nCOPY . .\nENTRYPOINT [\"dotnet\", \"{dll}\"]\n", ct);
            await Docker("publish", ct, "build", "-t", image, "-f", dockerfile, output);
        }
        return build;
    }

    public async Task<RunningCandidate> Run(string sha, CancellationToken ct)
    {
        Checked(sha);
        var given = await secrets(ct);
        var port = await AllocatePort(sha, ct);
        var network = NetworkOf(sha);
        try
        {
            await Docker("run", ct, "network", "create", network);
            await Docker("run", ct, "run", "-d", "--name", AzuriteOf(sha), "--network", network, "-p", $"{port + 5}:10000", AzuriteImage,
                "azurite", "--blobHost", "0.0.0.0", "--tableHost", "0.0.0.0", "--skipApiVersionCheck", "--loose");
            var postgresPassword = Secret();
            await Container("run", ct, new Dictionary<string, string> { ["POSTGRES_PASSWORD"] = postgresPassword, ["POSTGRES_DB"] = PostgresDatabase },
                "run", "-d", "--name", PostgresOf(sha), "--network", network,
                "--health-cmd", "pg_isready -U postgres", "--health-interval", "1s", "-e", "POSTGRES_PASSWORD", "-e", "POSTGRES_DB", PostgresImage);
            var clickHousePassword = Secret();
            await Container("run", ct, new Dictionary<string, string> { ["CLICKHOUSE_USER"] = "default", ["CLICKHOUSE_PASSWORD"] = clickHousePassword },
                "run", "-d", "--name", ClickHouseOf(sha), "--network", network,
                "--health-cmd", "clickhouse-client --query 'SELECT 1'", "--health-interval", "1s", "-e", "CLICKHOUSE_USER", "-e", "CLICKHOUSE_PASSWORD", ClickHouseImage);
            await dependenciesReady(sha, port + 5, ct);
            // Candidates boot from fresh stores; no partially copied live state or custody keys.
            var environment = Environment(sha, given.Model, postgresPassword, clickHousePassword);
            LastRunEnvironment = environment;
            await Container("run", ct, environment, ["run", "-d", "--name", ContainerOf(sha), "--hostname", ContainerOf(sha), "--network", network,
                "-p", $"{port}:5080", "--add-host", "host.docker.internal:host-gateway",
                .. environment.Keys.SelectMany(key => new[] { "-e", key }), ImageOf(sha)]);
            var mcp = McpEnvironment(sha);
            LastMcpEnvironment = mcp;
            await Container("run", ct, mcp, ["run", "-d", "--name", McpOf(sha), "--network", network,
                "-p", $"{port + 1}:5081", "--add-host", "host.docker.internal:host-gateway",
                .. mcp.Keys.SelectMany(key => new[] { "-e", key }), McpImageOf(sha)]);
            return new(sha, new CandidateEndpoints($"http://localhost:{port + 1}", $"http://localhost:{port}"), ContainerOf(sha));
        }
        catch
        {
            // Cleanup is best effort: a failing removal must not replace the error that ended the run.
            await Task.WhenAny(Remove(sha, CancellationToken.None));
            throw;
        }
    }

    // A readiness wait on fresh processes, bounded by the plan's budget; a stopped container fails at once.
    public async Task WaitHealthy(string sha, TimeSpan budget, CancellationToken ct)
    {
        Checked(sha);
        int port;
        lock (gate)
        {
            if (!ports.TryGetValue(sha, out port)) { throw new CandidateHostException("healthy", $"{sha} is not running on this host"); }
        }
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(budget);
        try
        {
            foreach (var (container, health) in new[] { (ContainerOf(sha), port), (McpOf(sha), port + 1) })
            {
                while (true)
                {
                    await Running("healthy", container, limit.Token);
                    try
                    {
                        using var response = await http.GetAsync(new Uri($"http://localhost:{health}/health"), limit.Token);
                        if (response.IsSuccessStatusCode) { break; }
                    }
                    catch (HttpRequestException)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), limit.Token);
                        continue;
                    }
                    await Task.Delay(TimeSpan.FromSeconds(1), limit.Token);
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"{ContainerOf(sha)} was not healthy within {budget}.");
        }
    }

    public async Task<string?> Commit(string sha, CancellationToken ct)
    {
        Checked(sha);
        try { return await Git(await workdir(ct)).Advance(LiveRef, sha, ct); }
        catch (InvalidOperationException error) { throw new CandidateHostException("commit", error.Message); }
    }

    public async Task Uncommit(string sha, string? previous, CancellationToken ct)
    {
        Checked(sha);
        try { await Git(await workdir(ct)).Retreat(LiveRef, sha, previous, ct); }
        catch (InvalidOperationException error) { throw new CandidateHostException("commit", error.Message); }
    }

    public async Task Remove(string sha, CancellationToken ct)
    {
        Checked(sha);
        // -v: postgres and clickhouse keep their data in anonymous volumes, which outlive a plain rm.
        await Ignoring("No such", ct, "rm", "-f", "-v", ContainerOf(sha), McpOf(sha), AzuriteOf(sha), PostgresOf(sha), ClickHouseOf(sha));
        await Ignoring("not found", ct, "network", "rm", NetworkOf(sha));
        lock (gate) { ports.Remove(sha); }
    }

    public async Task<string[]> Orphans(string[] known, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(known);
        var listed = await processes.RunAsync("docker", ["ps", "-a", "--filter", "name=candidate-", "--format", "{{.Names}}"], ".", null, ct);
        if (listed.ExitCode != 0) { throw new CandidateHostException("orphans", Tail(listed)); }
        var orphans = listed.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => (Name: name, Match: CandidateName().Match(name)))
            .Where(item => item.Match.Success && !known.Contains(item.Match.Groups["sha"].Value, StringComparer.Ordinal))
            .ToArray();
        foreach (var sha in orphans.Select(item => item.Match.Groups["sha"].Value).Distinct(StringComparer.Ordinal))
        { await Remove(sha, ct); }
        // A network can outlive its containers when a Run failed between `network create` and `docker run`.
        var networks = await processes.RunAsync("docker", ["network", "ls", "--filter", "name=candidate-", "--format", "{{.Name}}"], ".", null, ct);
        if (networks.ExitCode != 0) { throw new CandidateHostException("orphans", Tail(networks)); }
        var removedShas = orphans.Select(item => item.Match.Groups["sha"].Value).ToHashSet(StringComparer.Ordinal);
        var strayNetworks = networks.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => (Name: name, Match: CandidateNetwork().Match(name)))
            .Where(item => item.Match.Success && !known.Contains(item.Match.Groups["sha"].Value, StringComparer.Ordinal) && !removedShas.Contains(item.Match.Groups["sha"].Value))
            .Select(item => item.Name)
            .ToArray();
        foreach (var network in strayNetworks)
        { await Ignoring("not found", ct, "network", "rm", network); }
        return [.. orphans.Select(item => item.Name), .. strayNetworks];
    }

    // The environment the AppHost projects into the OS resource, re-pointed at the candidate's own
    // storage, databases and keys. Of the live Integrations only the model's values travel; every
    // other Integration the silo needs to boot is the candidate's own.
    internal Dictionary<string, string> Environment(string sha, IReadOnlyDictionary<string, string> model, string postgresPassword, string clickHousePassword)
    {
        var environment = Copied();
        foreach (var (key, value) in model) { environment[key] = value; }
        foreach (var (key, value) in OrleansIdentity(sha)) { environment[key] = value; }
        // The candidate silo composes the orleans module: its storage, clustering and reminders are the orleans
        // integration, pointed at the candidate's own Azurite (never Orleans__* provider keys).
        var storage = CandidateConnectionString(sha);
        foreach (var (key, value) in new Dictionary<string, string>
        {
            ["ASPNETCORE_URLS"] = "http://+:5080",
            [IntegrationVariable(OrleansIntegration, "Clustering")] = storage,
            [IntegrationVariable(OrleansIntegration, "GrainState")] = storage,
            [IntegrationVariable(OrleansIntegration, "Reminders")] = storage,
            [IntegrationVariable(OrleansIntegration, "GrainStateContainer")] = GrainStateContainer,
            [IntoChatOSConfiguration.EnvironmentName(IntoChatOSConfiguration.ModuleOptionsSection("apps") + ":Features")] = "/app/features",
            ["OTEL_SERVICE_NAME"] = $"candidate-{sha}",
            [Env(IntoChatOSNames.HomeKey)] = "/app/home",
            [Env(IntoChatOSNames.ConfiguredCustodyAcknowledgedConfigurationKey)] = "true",
            // The candidate owns fresh keys and a private container filesystem.
            [IntoChatOSNames.MasterKeyEnvironmentVariable] = Secret(),
            [IntegrationVariable("csharp", "RunTokenKey")] = Secret(),
            [IntegrationVariable("postgres", "ConnectionString")] =
                $"Host={PostgresOf(sha)};Port=5432;Username=postgres;Password={postgresPassword};Database={PostgresDatabase}",
            // The copied postgres options may place a database per brain, which needs the server's admin connection.
            [IntegrationVariable("postgres", "AdminConnection")] =
                $"Host={PostgresOf(sha)};Port=5432;Username=postgres;Password={postgresPassword}",
            // Supabase is postgres: a copied UseBackingStore option boots against the candidate's own server.
            [IntegrationVariable("supabase", "ConnectionString")] =
                $"Host={PostgresOf(sha)};Port=5432;Username=postgres;Password={postgresPassword};Database={PostgresDatabase}",
            [IntegrationVariable("clickhouse", "ConnectionString")] =
                $"Host={ClickHouseOf(sha)};Port=8123;Protocol=http;Username=default;Password={clickHousePassword};Database=default",
        })
        { environment[key] = value; }
        // Transcription may name a Foundry Local model, which runs on the developer's machine only: its native
        // library crashes the Linux container (seen live). Without it, transcription reports itself unavailable.
        environment.Remove(IntoChatOSConfiguration.EnvironmentName(IntoChatOSConfiguration.ModuleOptionsSection("ai") + ":Default:Transcription"));
        return environment;
    }

    // intochat-os-mcp is an Orleans client of the candidate silo: clustering and identity only.
    internal Dictionary<string, string> McpEnvironment(string sha)
    {
        var environment = Copied();
        var modules = Env(IntoChatOSConfiguration.ModulesSectionName) + "__";
        foreach (var key in environment.Keys.Where(key => key.StartsWith(modules, StringComparison.Ordinal)).ToArray())
        { environment.Remove(key); }
        foreach (var (key, value) in OrleansIdentity(sha)) { environment[key] = value; }
        // An Orleans client, not a silo: it finds the gateway through Azure table clustering and keeps its
        // cookie keys beside the grain state, both by connection string.
        var storage = CandidateConnectionString(sha);
        environment["Orleans__Clustering__ProviderType"] = "AzureTableStorage";
        environment["Orleans__Clustering__ServiceKey"] = IntoChatOSNames.Clustering;
        environment[ConnectionVariable(IntoChatOSNames.Clustering)] = storage;
        environment[ConnectionVariable(IntoChatOSNames.GrainState)] = storage;
        environment["ASPNETCORE_URLS"] = "http://+:5081";
        environment["OTEL_SERVICE_NAME"] = $"candidate-{sha}-mcp";
        return environment;
    }

    internal const string OtlpHttpEndpoint = IntoChat.OS.Modules.Microsoft.Aspire.CandidateEnvironment.OtlpHttpEndpoint;
    internal static readonly string[] ExportCadence = ["OTEL_BSP_SCHEDULE_DELAY", "OTEL_BLRP_SCHEDULE_DELAY", "OTEL_METRIC_EXPORT_INTERVAL"];

    private static string FromContainer(string url)
        => url.Replace("localhost", "host.docker.internal", StringComparison.OrdinalIgnoreCase).Replace("127.0.0.1", "host.docker.internal", StringComparison.Ordinal);

    private Dictionary<string, string> Copied()
    {
        var environment = live.AsEnumerable()
            .Where(pair => pair.Value is not null && Travels(pair.Key))
            .ToDictionary(pair => IntoChatOSConfiguration.EnvironmentName(pair.Key), pair => pair.Value!, StringComparer.Ordinal);
        environment["ASPNETCORE_ENVIRONMENT"] = live["ASPNETCORE_ENVIRONMENT"] ?? "Development";
        environment["DOTNET_ENVIRONMENT"] = live["DOTNET_ENVIRONMENT"] ?? live["ASPNETCORE_ENVIRONMENT"] ?? "Development";
        environment[Env(AuthOptions.PostureKey)] = "Open";
        // The candidate is an isolated container reached only through the parent host; the loopback-only rule for Open posture
        // does not apply, and the opt-in is explicit rather than implied by the container scope.
        environment[Env(AuthOptions.Key(nameof(AuthOptions.AllowOpenOnNetwork)))] = "true";
        // The dashboard's plain-HTTP OTLP endpoint when the AppHost opened one: its TLS endpoint's dev certificate
        // names localhost and is not trusted inside the container.
        if (live[OtlpHttpEndpoint] is { Length: > 0 } otlpHttp)
        {
            environment["OTEL_EXPORTER_OTLP_ENDPOINT"] = FromContainer(otlpHttp);
            environment["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf";
        }
        else if (live["OTEL_EXPORTER_OTLP_ENDPOINT"] is { Length: > 0 } otlp)
        {
            environment["OTEL_EXPORTER_OTLP_ENDPOINT"] = FromContainer(otlp);
            if (live["OTEL_EXPORTER_OTLP_PROTOCOL"] is { Length: > 0 } protocol) { environment["OTEL_EXPORTER_OTLP_PROTOCOL"] = protocol; }
        }
        if (live["OTEL_EXPORTER_OTLP_HEADERS"] is { Length: > 0 } headers) { environment["OTEL_EXPORTER_OTLP_HEADERS"] = headers; }
        // The export cadence travels too: a candidate verified seconds after boot must have exported its boot
        // spans by then, and the SDK's default 5s batch delay outlived the whole verification (seen live).
        foreach (var cadence in ExportCadence)
        { if (live[cadence] is { Length: > 0 } value) { environment[cadence] = value; } }
        return environment;
    }

    private const string OrleansIntegration = "orleans";

    // The candidate cluster's identity, shared by its silo and its MCP client.
    private static Dictionary<string, string> OrleansIdentity(string sha) => new(StringComparer.Ordinal)
    {
        ["Orleans__ServiceId"] = $"os-c{sha}",
        ["Orleans__ClusterId"] = $"os-c{sha}",
        ["Orleans__EnableDistributedTracing"] = "true",
    };

    private string GrainStateContainer => live[IntoChatOSNames.GrainStateContainerKey] is { Length: > 0 } named ? named : IntoChatOSNames.GrainStateContainer;

    private static bool Travels(string key) => CopiedSections.Any(section => key.StartsWith(section, StringComparison.OrdinalIgnoreCase));

    private async Task<int> AllocatePort(string sha, CancellationToken ct)
    {
        var listed = await processes.RunAsync("docker", ["ps", "--format", "{{.Ports}}"], ".", null, ct);
        if (listed.ExitCode != 0) { throw new CandidateHostException("run", Tail(listed)); }
        lock (gate)
        {
            for (var port = FirstPort; port < FirstPort + 1000; port += 10)
            {
                if (ports.ContainsValue(port)) { continue; }
                if (Enumerable.Range(port, 6).Any(p => listed.Output.Contains($":{p}->", StringComparison.Ordinal) || listening(p))) { continue; }
                ports[sha] = port;
                return port;
            }
        }
        throw new CandidateHostException("run", "no free candidate port block");
    }

    // Azurite, Postgres and ClickHouse were just started: wait (bounded) until each answers, failing at
    // once if one stops. The silo hard-fails at boot without them.
    private async Task DependenciesReady(string sha, int azuritePort, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(DependencyBudget);
        try
        {
            while (true)
            {
                await Running("run", AzuriteOf(sha), limit.Token);
                try
                {
                    using var response = await http.GetAsync(new Uri($"http://127.0.0.1:{azuritePort}/{CandidateStorage.AccountName}?comp=properties&restype=service"), limit.Token);
                    break;
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), limit.Token);
                    continue;
                }
            }
            foreach (var container in new[] { PostgresOf(sha), ClickHouseOf(sha) })
            {
                while (true)
                {
                    await Running("run", container, limit.Token);
                    var health = await processes.RunAsync("docker", ["inspect", "-f", "{{.State.Health.Status}}", container], ".", null, limit.Token);
                    if (health.Output.Trim() == "healthy") { break; }
                    await Task.Delay(TimeSpan.FromMilliseconds(500), limit.Token);
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new CandidateHostException("run", $"candidate {sha} storage and databases were not ready within {DependencyBudget}.");
        }
    }

    private async Task Running(string stage, string container, CancellationToken ct)
    {
        var state = await processes.RunAsync("docker", ["inspect", "-f", "{{.State.Running}}", container], ".", null, ct);
        if (state.ExitCode == 0 && state.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)) { return; }
        var logs = await processes.RunAsync("docker", ["logs", "--tail", "40", container], ".", null, CancellationToken.None);
        throw new CandidateHostException(stage, $"{container} stopped: {Tail(logs)}");
    }

    private static bool Listening(int port)
        => IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(endpoint => endpoint.Port == port);

    private static string? ProjectOf(string worktree, string file)
    {
        var full = Path.GetFullPath(worktree);
        for (var dir = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(full, file))); dir is not null && dir.Length > full.Length; dir = Path.GetDirectoryName(dir))
        {
            if (Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*.csproj").FirstOrDefault() is { } project) { return project; }
        }
        return null;
    }

    private async Task<string> Dotnet(string stage, string worktree, CancellationToken ct, params string[] args)
    {
        var result = await processes.RunAsync("dotnet", args, worktree, null, ct);
        if (result.ExitCode != 0) { throw new CandidateHostException(stage, $"dotnet {string.Join(' ', args)}:\n{Tail(result)}"); }
        return result.Output;
    }

    private Task Docker(string stage, CancellationToken ct, params string[] args) => Container(stage, ct, null, args);

    // Values travel through the docker CLI's own environment; `-e NAME` keeps secrets off the command line.
    private async Task Container(string stage, CancellationToken ct, IReadOnlyDictionary<string, string>? environment, params string[] args)
    {
        var result = await processes.RunAsync("docker", args, ".", environment, ct);
        if (result.ExitCode != 0) { throw new CandidateHostException(stage, $"docker {string.Join(' ', args)}:\n{Tail(result)}"); }
    }

    private async Task Ignoring(string absent, CancellationToken ct, params string[] args)
    {
        var result = await processes.RunAsync("docker", args, ".", null, ct);
        var errors = result.Error.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (result.ExitCode != 0 && (errors.Length == 0 || !errors.All(line => line.Contains(absent, StringComparison.OrdinalIgnoreCase))))
        { throw new CandidateHostException("remove", $"docker {string.Join(' ', args)}:\n{Tail(result)}"); }
    }

    private static string Tail(ProcessResult result, int lines = 40)
    {
        var all = (result.Output + "\n" + result.Error).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join('\n', all.TakeLast(lines));
    }

    private static async Task<string> Workdir(IModuleIntegrations<AspireModule> integrations, CancellationToken ct)
    {
        var snapshot = await integrations.Get("aspire").ReadAsync(ct);
        return snapshot.Settings.TryGetValue(AspireModule.WorkdirField, out var workdir) && workdir is { Length: > 0 }
            ? Path.GetFullPath(workdir)
            : throw new CandidateHostException("prepare", "the aspire Integration has no Workdir");
    }

    // The aspire Integration's two secrets, projected by AspireModuleHosting from the live resource's environment.
    private static async Task<CandidateSecrets> Secrets(IModuleIntegrations<AspireModule> integrations, CancellationToken ct)
    {
        var released = await integrations.Get("aspire").ReleaseAsync(ct);
        var model = released.Values.TryGetValue(AspireModule.CandidateModelField, out var json) && json is { Length: > 0 }
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? []
            : [];
        return new(model);
    }

    private Git Git(string root) => new(processes, Path.GetDirectoryName(Path.GetFullPath(root))!);

    private static string Secret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    internal static string Env(string key) => IntoChatOSConfiguration.EnvironmentName(key);
    internal static string ConnectionVariable(string name) => $"ConnectionStrings__{name}";
    internal static string IntegrationVariable(string id, string field) => IntoChatOSConfiguration.IntegrationEnvironmentName(id, field);

    // Promotion advances this ref, never a branch someone has checked out; people fast-forward their branch from it.
    public const string LiveRef = "refs/intochat-os/live";

    private static string CandidateConnectionString(string sha)
        => $"DefaultEndpointsProtocol=http;AccountName={CandidateStorage.AccountName};AccountKey={CandidateStorage.AccountKey};"
            + $"BlobEndpoint=http://{AzuriteOf(sha)}:10000/{CandidateStorage.AccountName};TableEndpoint=http://{AzuriteOf(sha)}:10002/{CandidateStorage.AccountName};";

    private static string WorktreeOf(string root, string sha) => Path.Combine(root, "worktrees", sha);
    private static string ImageOf(string sha) => $"intochat-os-candidate:{sha}";
    private static string McpImageOf(string sha) => $"intochat-os-candidate-mcp:{sha}";
    private static string ContainerOf(string sha) => $"candidate-{sha}";
    private static string McpOf(string sha) => $"candidate-{sha}-mcp";
    private static string AzuriteOf(string sha) => $"candidate-{sha}-azurite";
    private static string PostgresOf(string sha) => $"candidate-{sha}-postgres";
    private static string ClickHouseOf(string sha) => $"candidate-{sha}-clickhouse";
    private static string NetworkOf(string sha) => $"candidate-{sha}";

    [GeneratedRegex("^candidate-(?<sha>[0-9a-f]{7,40})(-azurite|-mcp|-postgres|-clickhouse)?$", RegexOptions.CultureInvariant)]
    private static partial Regex CandidateName();

    [GeneratedRegex("^candidate-(?<sha>[0-9a-f]{7,40})$", RegexOptions.CultureInvariant)]
    private static partial Regex CandidateNetwork();

    [GeneratedRegex("\\A[a-zA-Z0-9][a-zA-Z0-9._:/@-]*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ImageReference();

    [GeneratedRegex("^[0-9a-f]{7,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha();

    private static void Checked(string sha)
    {
        if (sha is null || !Sha().IsMatch(sha)) { throw new CandidateHostException("sha", $"'{sha}' is not a commit sha"); }
    }

}

internal sealed record CandidateSecrets(IReadOnlyDictionary<string, string> Model);
