using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using QdrantSdk = Qdrant;
namespace IntoChat.OS.Modules.Qdrant;

[DigitalBrain.Module(QdrantModule.ManifestName)]
public sealed class QdrantModule : DigitalBrain.Module
{
    internal const string ManifestName = "qdrant";

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<QdrantModuleOptions>(IntoChat.OS.Framework.Modules.Configuration.IntoChatOSConfiguration.ModuleOptionsSection(QdrantModule.ManifestName))],
        Secrets: []);

    public static bool RequiresContainer => true;

    public const string ConnectionName = "qdrant";

    public static bool IsConnected(IConfiguration configuration)
        => !string.IsNullOrWhiteSpace(configuration.GetConnectionString(ConnectionName));

    // Without a connection IVectorStore is served from memory, so search still works; nothing survives a restart.
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(_ => QdrantConnection.CreateClient(
            configuration.GetConnectionString(ConnectionName)!));
        services.TryAddSingleton<IVectorStore>(provider => IsConnected(configuration)
            ? new QdrantStore(provider.GetRequiredService<QdrantSdk.Client.QdrantClient>()) : new InMemoryQdrant());
    }
}
