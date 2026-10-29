using DigitalBrain.Testing;

namespace IntoChat.OS.Modules.Time.Tests;

// Unit tier: the Time neurons on TestOS only.
public sealed class TimeFixture : BothRuntimesFixture
{
    public static TheoryData<NeuronRuntimeKind> Kinds => new(NeuronRuntimeKind.TestOS);

    protected override IReadOnlyList<Type> Modules { get; } = [typeof(TimeModule)];
}
