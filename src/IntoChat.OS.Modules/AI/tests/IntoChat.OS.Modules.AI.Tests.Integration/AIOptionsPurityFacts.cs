using Xunit;

namespace IntoChat.OS.Modules.AI.Tests.Integration;

public sealed class AIOptionsPurityFacts
{
    [Fact]
    public void DefaultOptionsValidateWithoutExternalFacts()
    {
        new AIOptions().Validate();
    }

    // Options validate only their own values; whether a marker names a catalogued model
    // is the hosting ring's question, answered loudly at bind, not the option object's.
    [Fact]
    public void ValidateDoesNotConsultTheModelCatalog()
    {
        var options = new AIOptions();
        options.Hosting.Llms.Add("NotACataloguedModel");
        options.Hosting.Embeddings.Add("AlsoNotAModel");

        options.Validate();
    }
}
