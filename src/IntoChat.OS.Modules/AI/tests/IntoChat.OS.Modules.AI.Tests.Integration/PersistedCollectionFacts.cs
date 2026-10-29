using System.Text.Json;
using IntoChat.OS.Modules.AI.Agents;
using IntoChat.OS.Modules.AI.Inference;
using Xunit;

namespace IntoChat.OS.Modules.AI.Tests.Integration;

public sealed class PersistedCollectionFacts
{
    [Fact]
    public void AgentDefinitionCollectionsAndNullableOptionsRoundTripThroughJson()
    {
        var definition = new AgentDefinition
        {
            Contracts = ["IntoChat.OS.Modules.Apps.IApp"],
            Capabilities = ["research"],
            RoutingExamples = ["find evidence"],
            Contexts = ["research/agents/main/context"],
            Options = new(ProviderOptions: new Dictionary<string, string>() { ["mode"] = "precise" }),
        };
        var restored = JsonSerializer.Deserialize<AgentDefinition>(JsonSerializer.Serialize(definition))!;
        Assert.Equal(definition.Contracts, restored.Contracts);
        Assert.Equal(definition.Capabilities, restored.Capabilities);
        Assert.Equal(definition.RoutingExamples, restored.RoutingExamples);
        Assert.Equal(definition.Contexts, restored.Contexts);
        Assert.Equal("precise", restored.Options!.ProviderOptions!["mode"]);
        Assert.Null(JsonSerializer.Deserialize<InferenceOptions>("{}")!.ProviderOptions);
        Assert.Null(JsonSerializer.Deserialize<InferenceOptions>("{\"ProviderOptions\":null}")!.ProviderOptions);
        Assert.Empty(JsonSerializer.Deserialize<InferenceOptions>("{\"ProviderOptions\":{}}")!.ProviderOptions!);
    }
}
