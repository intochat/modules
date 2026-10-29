using DigitalBrain.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.ArtCraft.Tests;

// Unit tier: the module's plain neurons on TestOS only.
public sealed partial class ArtCraftFixture : BothRuntimesFixture
{
    public static TheoryData<NeuronRuntimeKind> Kinds => new(NeuronRuntimeKind.TestOS);

    protected override IReadOnlyList<Type> Modules { get; } = [typeof(ArtCraftModule)];

    protected override void ConfigureTestOS(IServiceCollection services) => ConfigureBoth(services);
}
