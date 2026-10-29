using DigitalBrain.Runtime.Orleans;
using DigitalBrain.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.AI.Tests;

// Integration tier: the plain AI neurons on TestOS and on SiloHost.Local, the model neurons over a gated fake inference.
public sealed class AiFixture : BothRuntimesFixture
{
    public static TheoryData<NeuronRuntimeKind> Kinds => NeuronRuntimeKinds.Both;

    public GatedInference Inference { get; } = new();

    protected override IReadOnlyList<Type> Modules { get; } = [typeof(AIModule)];

    protected override void ConfigureTestOS(IServiceCollection services)
        => services.AddSingleton<IntoChat.OS.Modules.AI.Models.IModelInference>(Inference);

    // The AI neurons' [Signal] records live in the contracts assembly, beside the module's.
    protected override void ConfigureSilo(IServiceCollection services)
    {
        services.AddJournalPayloads(typeof(IntoChat.OS.Modules.AI.Agents.Facts.IFacts).Assembly);
        services.AddSingleton<IntoChat.OS.Modules.AI.Models.IModelInference>(Inference);
    }
}
