using DigitalBrain.Capabilities;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Modules.Postgres.Tables;

namespace IntoChat.OS.Modules.Postgres.Tests.Integration.Tables;

public sealed class TableOwnerFacts
{
    [Fact]
    public void AnOwnerRoundTripsThroughItsGrainKey()
    {
        var owner = new TableOwner("brain-1", "brain-1/apps/notes");
        Assert.Equal(owner, TableOwner.Parse(owner.Key));
        Assert.Equal("brain-1/tables/brain-1%2Fapps%2Fnotes", owner.Key);
    }

    [Theory]
    [InlineData("brain")]
    [InlineData("brain/tables")]
    [InlineData("brain/tables/app/extra")]
    [InlineData("/tables/app")]
    [InlineData("brain/tables/")]
    [InlineData("brain/apps/app")]
    [InlineData("brain/tables/a%2fb")]
    public void AMalformedKeyIsRefusedAsUnauthorized(string key)
        => Assert.Throws<UnauthorizedAccessException>(() => TableOwner.Parse(key));

    [Fact]
    public void TheCurrentOwnerComesFromTheStampedCallerAndRequiresAnApp()
    {
        var caller = CapabilityCallers.Of("alice", "alice", "brain-1", CallerKind.App, "brain-1/apps/notes");
        // Grain keys carry the brain scope id, never the caller's raw brain name.
        Assert.Equal(new TableOwner(BrainScope.Create("alice", "brain-1").Id, "brain-1/apps/notes"), TableOwner.Current(caller));
        Assert.Throws<UnauthorizedAccessException>(() => TableOwner.Current(caller with { AppId = "" }));
        Assert.Throws<UnauthorizedAccessException>(() => TableOwner.Current(new CapabilityCaller("", "alice", "brain-1", "brain-1/apps/notes")));
    }
}
