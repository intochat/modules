using DigitalBrain.Runtime.Orleans;
using DigitalBrain.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Mcp.Tests.Integration;

// Integration tier: the module's plain neurons on TestOS and on SiloHost.Local (06-06d).
public sealed class McpFixture : BothRuntimesFixture
{
    public static TheoryData<NeuronRuntimeKind> Kinds => NeuronRuntimeKinds.Both;

    protected override IReadOnlyList<Type> Modules { get; } = [typeof(McpModule)];

    // The neuron's [Signal] records live in the contracts assembly.
    protected override void ConfigureSilo(IServiceCollection services) => services.AddJournalPayloads(typeof(IMcpServer).Assembly);
}
