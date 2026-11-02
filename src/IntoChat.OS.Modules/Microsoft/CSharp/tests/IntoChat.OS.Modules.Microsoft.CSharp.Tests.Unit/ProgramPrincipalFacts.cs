using DigitalBrain.Capabilities;
using IntoChat.OS.Framework;
using IntoChat.OS.Modules.Microsoft.CSharp.Programs;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Unit;

public sealed class ProgramPrincipalFacts
{
    private static readonly IServiceProvider NoServices = new NoServiceProvider();

    [Fact]
    public async Task ThePersonWhoDeployedIsTheProgramsPerson()
    {
        var owner = CapabilityCallers.Of("ada", "ada-account", "ada");
        using var stamp = CallerContextStamper.StampScope(owner);
        Assert.Equal(owner, await ProgramPrincipal.ForBrain(NoServices, "ada", null));
    }

    [Fact]
    public async Task AHeldPersonIsKeptWithoutADirectory()
    {
        var held = CapabilityCallers.Of("ada", "ada-account", "ada");
        Assert.Equal(held, await ProgramPrincipal.ForBrain(NoServices, "ada", held));
    }

    [Fact]
    public void TheBrainIdStampThePlatformAndAnotherBrainAreNotPersons()
    {
        Assert.False(ProgramPrincipal.IsPerson(CapabilityCallers.Of("ada", "ada", "ada"), "ada"));
        Assert.False(ProgramPrincipal.IsPerson(CapabilityCallers.Platform("ada", "ada", "ada"), "ada"));
        Assert.False(ProgramPrincipal.IsPerson(CapabilityCallers.Of("ada", "ada-account", "other"), "ada"));
        Assert.True(ProgramPrincipal.IsPerson(CapabilityCallers.Of("ada", "ada-account", "ada", CallerKind.App, "ada/apps/notes"), "ada"));
    }

    private sealed class NoServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
