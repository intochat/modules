using DigitalBrain.Capabilities;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Modules.AI.Models;

namespace IntoChat.OS.Modules.AI.Tests.Unit;

// /ai/models lists the caller's brain when the edge stamped a valid one, and the deployment's list
// otherwise: an identified caller without a valid brain must not turn into a 500.
public sealed class ModelRouteKeyFacts
{
    private static CapabilityCaller Caller(string brainId) => new("person-1", "account-1", brainId);

    [Fact]
    public void A_valid_brain_lists_that_brain() => Assert.Equal("brain-1/main", AIModule.CatalogKey(Caller("brain-1")));

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("a:b")]
    public void A_caller_without_a_valid_brain_gets_the_deployment_list(string brainId)
        => Assert.Equal(IModelCatalog.DeploymentKey, AIModule.CatalogKey(Caller(brainId)));

    [Fact]
    public void No_caller_gets_the_deployment_list() => Assert.Equal(IModelCatalog.DeploymentKey, AIModule.CatalogKey(null));
}
