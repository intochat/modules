using DigitalBrain.Capabilities.Storage;
using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Unit;

public sealed class AppRunnerFacts
{
    [Fact]
    public void ResolveCapability_ThrowsUnauthorizedAccessException_WhenCapabilityNotConsented()
    {
        var manifest = new AppManifest(
            Title: "UntrustedApp",
            Description: "An untrusted app without declared capabilities.",
            Operations: [],
            Settings: [],
            Slots: [],
            Requires: new AppRequirements(
                SemanticTypes: [],
                Connectors: [],
                Secrets: [],
                Capabilities: []));

        var snapshot = new AppSnapshot(
            Name: "untrusted",
            Manifest: manifest,
            Spec: "",
            Files: [],
            Settings: [],
            Connections: [],
            Version: "1.0.0",
            Status: AppStatus.Live,
            Activity: [],
            Versions: []);

        var ex = Assert.Throws<UnauthorizedAccessException>(() =>
            AppRunner.ResolveCapability(brain: null!, snapshot, typeof(IStorage)));

        Assert.Contains("Security Violation", ex.Message);
        Assert.Contains("UntrustedApp", ex.Message);
        Assert.Contains("storage@1", ex.Message);
    }
}
