using DigitalBrain.Capabilities.Connections.Connectors;
using DigitalBrain.Modules.Configuration;
using DigitalBrain.Modules.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using IntoChat.OS.Modules.Postgres.Tables;
using IntoChat.OS.Modules.Postgres.Tables.Signals;

namespace IntoChat.OS.Modules.Postgres;

[DigitalBrain.Module("postgres")]
public sealed class PostgresModule : DigitalBrain.Module
{
    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<PostgresModuleOptions>(ModuleConfiguration.ModuleOptionsSection(new ModuleId("postgres")))],
        Secrets: []);

    public static bool RequiresContainer => true;

    public static System.Reflection.Assembly? Contracts => typeof(IPostgresServer).Assembly;

    public static IntegrationSchema Integration => IntegrationSchema.For("postgres", "Postgres").RequiresSecret("ConnectionString").OffersSecret("AdminConnection");
    public static ConnectorDefinition Connector => new("postgres", "Postgres", ["connection-string"], ["postgres"]) { Neurons = [typeof(IPostgresServer), typeof(ITable)], Signals = [typeof(TableDefined), typeof(RowUpserted), typeof(RowDeleted)] };

    public const string ConnectionName = "postgres";
    public const string ProviderName = "Npgsql";

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddPostgres();
    }
}
