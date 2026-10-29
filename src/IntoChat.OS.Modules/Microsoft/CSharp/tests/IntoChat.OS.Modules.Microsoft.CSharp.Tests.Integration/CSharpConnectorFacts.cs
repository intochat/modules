using IntoChat.OS.Modules.Microsoft.CSharp.Files;
using IntoChat.OS.Modules.Microsoft.CSharp.Files.Signals;
using Xunit;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

// The sandbox is declared as a connector so the platform's catalog knows what it is the external system
// behind. It is not offered to users: no connection neuron serves it (SessionTokensFacts pins that side).
public sealed class CSharpConnectorFacts
{
    [Fact]
    public void TheConnectorNamesTheFileNeuronAndItsPublicSignal()
    {
        var connector = CSharpModule.Connector;
        Assert.Equal("csharp", connector.Id);
        Assert.Equal([typeof(ICSharpFile)], connector.Neurons);
        Assert.Equal([typeof(CSharpFileChanged)], connector.Signals);
        Assert.Empty(connector.RequiredIntegrations);
        Assert.Equal(["csharp"], connector.OptionalIntegrations);
    }

}
