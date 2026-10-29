using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Framework.Configuration;
using IntoChat.OS.Framework.Modules.Configuration;

namespace IntoChat.OS.Modules.Microsoft.CSharp;

public sealed class CSharpOptions : IModuleOptions
{
    // The agent tools, the registry entry and the /csharp console routes. The runtime edge and the compile
    // gate the Apps module uses are not authoring and stay on either way.
    public bool Authoring { get; set; } = true;
    // Signals buffered per subscription while nothing drains; past it the oldest is dropped and counted.
    public int MaximumPending { get; set; } = 64;
    // How long an app's rules may take to judge one request; past it the request is rejected.
    public TimeSpan RuleTimeout { get; set; } = TimeSpan.FromSeconds(3);

    public static readonly string SectionName = IntoChatOSConfiguration.ModuleOptionsSection(new ModuleId("csharp"));

    // Deployment-owned values; the deployment sets them, a user never edits them.
    // The runtime edge URL sessions dial; development derives it from this brain's own HTTP address.
    [Setting]
    public string? EdgeUrl { get; set; }
    // Production: the Azure Container Apps session pool management endpoint. Set, it replaces the local container.
    [Setting]
    public string? SessionPoolEndpoint { get; set; }
    // Development: the always-on csharp-sandbox container that hosts every brain's session.
    [Setting]
    public string? SandboxUrl { get; set; }

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumPending, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(RuleTimeout, TimeSpan.Zero);
    }
}
