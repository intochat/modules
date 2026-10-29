using IntoChat.OS.Framework.Modules;
using Microsoft.AspNetCore.Routing;

namespace IntoChat.OS.Modules.LocalDisk;

[DigitalBrain.Module(ManifestName)]
public sealed class LocalDiskModule : DigitalBrain.Module, IHttpModule
{
    internal const string ManifestName = "local-disk";

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [DigitalBrain.Capabilities.Capability.Storage.V1],
        Requires: [],
        Needs: [],
        Settings: [],
        Secrets: []);

    public void Configure(IEndpointRouteBuilder endpoints) => LocalDiskEndpoints.Map(endpoints);
}
