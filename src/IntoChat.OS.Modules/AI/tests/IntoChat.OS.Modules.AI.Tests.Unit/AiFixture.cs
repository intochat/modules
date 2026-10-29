using DigitalBrain.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.AI.Tests;

// Unit tier: the plain AI neurons on TestOS only, the model neurons over a gated fake inference.
public sealed class AiFixture : BothRuntimesFixture
{
    public static TheoryData<NeuronRuntimeKind> Kinds => new(NeuronRuntimeKind.TestOS);

    public GatedInference Inference { get; } = new();

    protected override IReadOnlyList<Type> Modules { get; } = [typeof(AIModule)];

    protected override void ConfigureTestOS(IServiceCollection services)
        => services.AddSingleton<IntoChat.OS.Modules.AI.Models.IModelInference>(Inference);
}
