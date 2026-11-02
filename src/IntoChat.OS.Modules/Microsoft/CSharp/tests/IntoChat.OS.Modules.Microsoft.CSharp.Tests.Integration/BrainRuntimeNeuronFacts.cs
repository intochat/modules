using DigitalBrain.Runtime.Orleans;
using DigitalBrain.Testing;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Framework;
using IntoChat.OS.Modules.Microsoft.CSharp.Assemblies;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
using IntoChat.OS.Modules.Microsoft.CSharp.Files;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;
using IntoChat.OS.Modules.Microsoft.CSharp.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

public sealed class BrainRuntimeNeuronFixture : BothRuntimesFixture
{
    public static TheoryData<NeuronRuntimeKind> Kinds => new() { NeuronRuntimeKind.Silo };

    protected override IReadOnlyList<Type> Modules { get; } = [typeof(CSharpModule)];

    protected override void ConfigureSilo(IServiceCollection services) => ConfigureDependencies(services);

    protected override void ConfigureTestOS(IServiceCollection services) => ConfigureDependencies(services);

    private static void ConfigureDependencies(IServiceCollection services)
    {
        services.AddLogging();
        services.AddJournalPayloads(typeof(ICSharpFile).Assembly);
        services.TryAddSingleton<CSharpScriptCheck>();
        services.TryAddSingleton<IBehaviorAssemblies, MemoryBehaviorAssemblies>();
        services.TryAddSingleton(new CompiledTypes([typeof(ICSharpFile).Assembly]));

        var fakeHost = new FakeSessionHost();
        var options = Options.Create(new CSharpOptions { SandboxUrl = FakeSessionHost.Url });
        services.TryAddSingleton<IOptions<CSharpOptions>>(options);
        services.TryAddSingleton(new SessionRunner(new HttpClient(fakeHost), options));

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["csharp:SandboxUrl"] = FakeSessionHost.Url,
            ["IntoChatOS:Topology"] = "local",
        }).Build();

        var tokens = new SessionTokens(
            new IdleIntegrations(),
            TimeProvider.System,
            new TestHostEnv(),
            config,
            NullLogger<SessionTokens>.Instance);
        services.TryAddSingleton(tokens);

        services.TryAddKeyedSingleton<IHostApplicationLifetime>(typeof(CSharpModule), new TestLifetime());
        services.TryAddSingleton<DeferredRuntimeStarts>();
        services.TryAddSingleton<SessionStreams>();
        services.TryAddSingleton<RuntimeDirectory>();
        services.TryAddSingleton(TimeProvider.System);
    }

    private sealed class IdleIntegrations : IModuleIntegrations<CSharpModule>
    {
        public IIntegrationHandle Get(string integrationId) => new Idle();

        private sealed class Idle : IIntegrationHandle
        {
            public string IntegrationId => "csharp";
            public Task<IntegrationSnapshot> ReadAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(new IntegrationSnapshot { IntegrationId = "csharp", Status = IntegrationStatus.Unconfigured });
            public Task<string> ReadSecretAsync(string field, CancellationToken cancellationToken = default) => throw new InvalidOperationException(field);
            public Task<ReleasedIntegration> ReleaseAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        }
    }

    private sealed class TestHostEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public global::Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new global::Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class TestLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
}

public sealed class BrainRuntimeNeuronFacts(BrainRuntimeNeuronFixture fixture) : IClassFixture<BrainRuntimeNeuronFixture>
{
    public static TheoryData<NeuronRuntimeKind> Kinds => BrainRuntimeNeuronFixture.Kinds;

    private const string ValidAppSource = """
        [App("hello")]
        public sealed class HelloApp : DigitalBrain.App
        {
            public static void Run()
            {
                Console.WriteLine("Hello from BrainRuntimeNeuron");
            }
        }
        """;

    [Theory, MemberData(nameof(Kinds))]
    public async Task AssigningAndRetiringAppProgramCompletesCleanlyWithoutContextLoss(NeuronRuntimeKind kind)
    {
        var runtime = await fixture.Runtime(kind);
        var runtimeId = runtime.Id(BrainRuntimeNeuron.GrainTypeName, BrainRuntimeKeys.Name);
        var programs = runtime.Get<IBrainPrograms>(runtimeId);

        var owner = $"csharp/remediation-{kind}";
        var sources = new Dictionary<string, string>
        {
            ["app.cs"] = ValidAppSource,
        };
        var settings = new Dictionary<string, string>
        {
            ["Color"] = "blue",
        };

        // Assign program to BrainRuntimeNeuron
        var view = await programs.Assign(owner, sources, settings);
        Assert.NotNull(view);
        Assert.NotEmpty(view.Hash);
        Assert.Single(view.Behaviors);

        var described = await programs.Describe(owner);
        Assert.NotNull(described);

        // Retire program: verifies Orleans grain context is preserved,
        // EndSessionAsync executes cleanly, ReminderNames check succeeds,
        // and no InvalidOperationException is thrown.
        await programs.Retire(owner);

        var describedAfter = await programs.Describe(owner);
        Assert.Null(describedAfter);
    }
}
