using DigitalBrain.Runtime.Orleans;
using IntoChat.OS.Modules.Microsoft.CSharp.Files;
using IntoChat.OS.Modules.Microsoft.CSharp.Files.Signals;
using DigitalBrain.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

// The module's plain neurons on TestOS and on SiloHost.Local (06-06d).
public sealed class CSharpFixture : BothRuntimesFixture
{
    public static TheoryData<NeuronRuntimeKind> Kinds => NeuronRuntimeKinds.Both;

    protected override IReadOnlyList<Type> Modules { get; } = [typeof(CSharpModule)];

    // The neurons' [Signal] records live in the contracts assembly.
    protected override void ConfigureSilo(IServiceCollection services) => services.AddJournalPayloads(typeof(ICSharpFile).Assembly);
}

// The plain C# file (06-06d) on both runtimes (R4-05): what it keeps and publishes before it runs. Starting, stopping and deleting
// reach the still-legacy brain runtime, which only the kernel composition hosts; CSharpFileSnapshotFacts cover them there.
public sealed class CSharpFileRuntimeFacts(CSharpFixture fixture) : IClassFixture<CSharpFixture>
{
    public static TheoryData<NeuronRuntimeKind> Kinds => CSharpFixture.Kinds;

    [Theory, MemberData(nameof(Kinds))]
    public async Task AWrittenFileKeepsItsSourceSettingsAndDescriptionAndPublishesEachChange(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("microsoft.csharp.file", $"csharp/written-{kind}");
        var file = runtime.Get<ICSharpFile>(id);

        await file.Write("Console.WriteLine(1);", ct);
        await file.Configure(new Dictionary<string, string> { ["Mode"] = "fast" }, ct);
        var described = await file.Describe(null, "  prints one  ", ct);

        Assert.Equal(("Console.WriteLine(1);", "fast", $"written-{kind}", "prints one", "Stopped", false),
            (described.Source, described.Settings["Mode"], described.Name, described.Purpose, described.Status, described.ShouldRun));
        Assert.NotNull(described.Created);
        var changes = await runtime.WaitForSignals<CSharpFileChanged>(id, 3, TimeSpan.FromSeconds(20), ct);
        Assert.All(changes, changed => Assert.Equal($"{runtime.Brain.Value}/csharp/written-{kind}", changed.FileId));
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task ANeverWrittenFileCannotBeDescribedAndBadSettingsAreRefused(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var file = runtime.Get<ICSharpFile>(runtime.Id("microsoft.csharp.file", $"csharp/never-{kind}"));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => file.Describe("name", null, ct));
        await Assert.ThrowsAsync<ArgumentException>(() => file.Configure(new Dictionary<string, string> { ["bad name"] = "x" }, ct));
        await Assert.ThrowsAsync<ArgumentException>(() => file.Write(new string('x', CSharpFileNeuron.MaximumSourceBytes + 1), ct));
    }
}
