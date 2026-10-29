namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

// An owner-scoped neuron exercises the real call filter's recorded-connection exception. A plain neuron (06-08), hosted by the
// test's DigitalBrain module (PingerNeurons).
[DigitalBrain.Contracts.NeuronAlias("tests.csharp.connection-probe"), DigitalBrain.Contracts.DefaultNeuronType(ConnectionProbe.Type)]
public interface IConnectionProbe
{
    Task<int> Read();
}

[DigitalBrain.Neuron(Type)]
public sealed class ConnectionProbe : DigitalBrain.Neuron, IConnectionProbe
{
    public const string Type = "tests.csharp.connection-probe";

    public Task<int> Read() => Task.FromResult(42);
}
