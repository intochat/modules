using DigitalBrain.Runtime.Orleans;
using DigitalBrain.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Flutter.Tests;

// Integration tier: the plain Flutter controls on TestOS and on SiloHost.Local.
public sealed class FlutterFixture : BothRuntimesFixture
{
    public static TheoryData<NeuronRuntimeKind> Kinds => NeuronRuntimeKinds.Both;

    protected override IReadOnlyList<Type> Modules { get; } = [typeof(FlutterModule)];

    // The controls' [Signal] records live in the contracts assembly, beside the module's.
    protected override void ConfigureSilo(IServiceCollection services)
        => services.AddJournalPayloads(typeof(Button.IButton).Assembly);
}
