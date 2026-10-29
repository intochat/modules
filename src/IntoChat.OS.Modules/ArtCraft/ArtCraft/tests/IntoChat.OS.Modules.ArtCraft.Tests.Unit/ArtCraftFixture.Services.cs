using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.ArtCraft.Tests;

public sealed partial class ArtCraftFixture
{
    // A deployment that serves PhotoCraft's page but no MCP server for either app.
    private static void ConfigureBoth(IServiceCollection services)
        => services.AddOptions<ArtCraftModuleOptions>().Configure(options => options.PhotoCraftWeb = "http://localhost:18110/");
}
