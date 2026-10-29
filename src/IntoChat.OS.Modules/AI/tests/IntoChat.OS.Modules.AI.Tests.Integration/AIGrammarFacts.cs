using Microsoft.Extensions.Configuration;
using Xunit;

namespace IntoChat.OS.Modules.AI.Tests.Integration;

public sealed class AIGrammarFacts
{
    [Trait("host", "own")]
    [Fact]
    public void ModuleOptionsSectionBindsTheOptions()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["IntoChatOS:Modules:ai:Options:Default:Model"] = "Fable5" }).Build();

        Assert.Equal("Fable5", AIOptionsRuntime.Read(configuration).Default.Model);
    }
}
