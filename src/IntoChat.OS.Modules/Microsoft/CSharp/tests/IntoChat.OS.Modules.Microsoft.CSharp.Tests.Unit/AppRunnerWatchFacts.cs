using DigitalBrain;
using IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Unit;

public sealed class AppRunnerWatchFacts
{
    [Fact]
    public void AddressOf_TakesTheBrainFromTheAppKey()
        => Assert.Equal(new AppAddress("owner", "owner/apps/settings"), AppRunner.AddressOf("owner/apps/settings", null));

    [Fact]
    public void AddressOf_PrefersTheBrainSetting()
        => Assert.Equal(new AppAddress("circle-1", "owner/apps/settings"), AppRunner.AddressOf("owner/apps/settings", "circle-1"));

    [Fact]
    public void AddressOf_RejectsAKeyThatIsNotAnAppKey()
        => Assert.Throws<InvalidOperationException>(() => AppRunner.AddressOf("owner/settings", null));
}
