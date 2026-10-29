using DigitalBrain.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Microsoft.Roslyn.Tests;

// Unit tier: the module's plain neurons on TestOS only.
public sealed partial class RoslynFixture : BothRuntimesFixture
{
    public static TheoryData<NeuronRuntimeKind> Kinds => new(NeuronRuntimeKind.TestOS);

    protected override IReadOnlyList<Type> Modules { get; } = [typeof(RoslynModule)];

    protected override void ConfigureTestOS(IServiceCollection services) => ConfigureBoth(services);
}
