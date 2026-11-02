using System.Security.Cryptography;
using System.Text;
using DigitalBrain.Runtime.Orleans;
using DigitalBrain.Testing;
using IntoChat.OS.Modules.Microsoft.CSharp.Assemblies;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
using IntoChat.OS.Modules.Microsoft.CSharp.Files;
using IntoChat.OS.Modules.Microsoft.CSharp.Programs;
using IntoChat.OS.Modules.Microsoft.CSharp.Programs.Signals;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration.Neurons;

public sealed class AppProgramFixture : BothRuntimesFixture
{
    public static TheoryData<NeuronRuntimeKind> Kinds => NeuronRuntimeKinds.Both;

    protected override IReadOnlyList<Type> Modules { get; } = [typeof(CSharpModule)];

    protected override void ConfigureSilo(IServiceCollection services)
    {
        services.AddJournalPayloads(typeof(ICSharpFile).Assembly);
        services.TryAddSingleton<CSharpScriptCheck>();
        services.TryAddSingleton<IBehaviorAssemblies, MemoryBehaviorAssemblies>();
    }

    protected override void ConfigureTestOS(IServiceCollection services)
    {
        services.TryAddSingleton<CSharpScriptCheck>();
        services.TryAddSingleton<IBehaviorAssemblies, MemoryBehaviorAssemblies>();
    }
}

public sealed class AppProgramNeuronFacts(AppProgramFixture fixture) : IClassFixture<AppProgramFixture>
{
    public static TheoryData<NeuronRuntimeKind> Kinds => AppProgramFixture.Kinds;

    private const string ValidAppSource = """
        [App("hello")]
        public sealed class HelloApp : DigitalBrain.App
        {
            public static void Run()
            {
                Console.WriteLine("Hello from AppProgramNeuron");
            }
        }
        """;

    [Theory, MemberData(nameof(Kinds))]
    public async Task AssigningCodeProducesValidProgramViewAndPublishesSignal(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("microsoft.csharp.app-program", $"app/assign-{kind}");
        var program = runtime.Get<IAppProgram>(id);

        var sources = new Dictionary<string, string>
        {
            ["app.cs"] = ValidAppSource,
        };
        var settings = new Dictionary<string, string>
        {
            ["Color"] = "blue",
        };

        var view = await program.Assign(sources, settings);
        Assert.NotNull(view);
        Assert.NotEmpty(view.Hash);
        Assert.Single(view.Behaviors);
        Assert.Equal("Running", view.Behaviors[0].Status);

        var described = await program.Describe();
        Assert.NotNull(described);
        Assert.Equal(view.Hash, described.Hash);
        Assert.Single(described.Behaviors);
        Assert.Equal(view.Behaviors[0].Hash, described.Behaviors[0].Hash);

        var signals = await runtime.WaitForSignals<AppProgramChanged>(id, 1, TimeSpan.FromSeconds(20), ct);
        Assert.NotEmpty(signals);
        Assert.Equal(AppProgramKeys.For(runtime.Brain.Value, $"assign-{kind}"), signals[0].AppKey);
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task RetiringAnAppTransitionsStatusAndPublishesSignal(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("microsoft.csharp.app-program", $"app/retire-{kind}");
        var program = runtime.Get<IAppProgram>(id);

        await program.Assign(new Dictionary<string, string> { ["app.cs"] = ValidAppSource }, []);

        var beforeRetire = await program.Describe();
        Assert.NotNull(beforeRetire);
        Assert.Equal("Running", beforeRetire.Behaviors[0].Status);

        await program.Retire();

        var afterRetire = await program.Describe();
        Assert.NotNull(afterRetire);
        Assert.Equal("Stopped", afterRetire.Behaviors[0].Status);

        var signals = await runtime.WaitForSignals<AppProgramChanged>(id, 2, TimeSpan.FromSeconds(20), ct);
        Assert.True(signals.Count >= 2);
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task AcknowledgeUpdatesWatermarkAndTelemetry(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("microsoft.csharp.app-program", $"app/ack-{kind}");
        var program = runtime.Get<IAppProgram>(id);

        await program.Assign(new Dictionary<string, string> { ["app.cs"] = ValidAppSource }, []);

        await program.Acknowledge(watermark: 100, cpuMicroseconds: 5000, wallMicroseconds: 12000);

        var signals = await runtime.WaitForSignals<AppProgramChanged>(id, 2, TimeSpan.FromSeconds(20), ct);
        Assert.True(signals.Count >= 2);
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task AnUnassignedAppReturnsNullDescription(NeuronRuntimeKind kind)
    {
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("microsoft.csharp.app-program", $"app/unassigned-{kind}");
        var program = runtime.Get<IAppProgram>(id);

        var described = await program.Describe();
        Assert.Null(described);
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task AssigningInvalidCodeThrowsAndLeavesAppUnassigned(NeuronRuntimeKind kind)
    {
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("microsoft.csharp.app-program", $"app/invalid-{kind}");
        var program = runtime.Get<IAppProgram>(id);

        var invalidSources = new Dictionary<string, string>
        {
            ["broken.cs"] = "public class NotAnApp { }",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => program.Assign(invalidSources, []));

        var described = await program.Describe();
        Assert.Null(described);
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task AssignPrebuiltWithValidAssemblySucceeds(NeuronRuntimeKind kind)
    {
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("microsoft.csharp.app-program", $"app/prebuilt-{kind}");
        var program = runtime.Get<IAppProgram>(id);

        var fakeAssemblyBytes = Encoding.UTF8.GetBytes("fake assembly dll content for testing prebuilt assignment");
        var hash = Convert.ToHexStringLower(SHA256.HashData(fakeAssemblyBytes));
        var prebuilt = new PrebuiltBehavior(fakeAssemblyBytes, [], hash);

        var behaviors = new Dictionary<string, PrebuiltBehavior>
        {
            ["main"] = prebuilt,
        };

        var view = await program.AssignPrebuilt(behaviors, new Dictionary<string, string> { ["Env"] = "prod" });
        Assert.NotNull(view);
        Assert.NotEmpty(view.Hash);
        Assert.Single(view.Behaviors);
        Assert.Equal(hash, view.Behaviors[0].Hash);

        var described = await program.Describe();
        Assert.NotNull(described);
        Assert.Equal(view.Hash, described.Hash);
    }
}
