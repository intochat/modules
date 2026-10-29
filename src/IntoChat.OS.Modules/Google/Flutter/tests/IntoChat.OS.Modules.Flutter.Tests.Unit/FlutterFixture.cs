using DigitalBrain.Testing;

namespace IntoChat.OS.Modules.Flutter.Tests;

// Unit tier: the plain Flutter controls on TestOS only.
public sealed class FlutterFixture : BothRuntimesFixture
{
    public static TheoryData<NeuronRuntimeKind> Kinds => new(NeuronRuntimeKind.TestOS);

    protected override IReadOnlyList<Type> Modules { get; } = [typeof(FlutterModule)];
}
