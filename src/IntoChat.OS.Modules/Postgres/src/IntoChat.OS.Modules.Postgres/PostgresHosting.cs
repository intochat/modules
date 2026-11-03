using DigitalBrain.Capabilities.Connections;
using DigitalBrain.Capabilities.Connections.Connectors;
using DigitalBrain.Capabilities.Connections.Integrations;
using IntoChat.OS.Modules.Postgres.Tables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IntoChat.OS.Modules.Postgres;

public static class PostgresHosting
{
    public const string DataSourceKey = "IntoChat.OS.Modules.Postgres";

    public static IServiceCollection AddPostgres(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(PostgresRegistration))) { return services; }
        services.AddSingleton<PostgresRegistration>();
        services.AddOptions<PostgresModuleOptions>()
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionName), "Postgres requires a connection name.")
            .ValidateOnStart();
        // Server
        services.TryAddSingleton<PostgresSourceRegistry>();
        services.TryAddSingleton<IPostgresSourceRegistry>(provider => provider.GetRequiredService<PostgresSourceRegistry>());
        services.AddSingleton<IIntegrationStartup>(provider => provider.GetRequiredService<PostgresSourceRegistry>());
        services.TryAddSingleton<IPostgresProvider, IntegrationPostgresProvider>();
        services.TryAddSingleton<IConnectionPostgresProviderFactory, ConnectionPostgresProviderFactory>();
        services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);
        // Tables
        services.TryAddSingleton<ITableStore, TableStore>();
        services.TryAddSingleton(provider => new BrainDatabases(provider.GetRequiredService<IModuleIntegrations<PostgresModule>>().Get("postgres")));
        services.TryAddSingleton<ITablesSlot, NullTablesSlot>();
        services.TryAddSingleton<ITablePlacements, TablePlacements>();
        services.TryAddSingleton<IConnectionTableStores, ConnectionTableStores>();
        services.AddSingleton<IIncomingGrainCallFilter, TableLifecycleGuard>();
        return services;
    }

    private sealed class PostgresRegistration;

    private sealed class PostgresHealthCheck(IPostgresProvider provider) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var connection = await provider.PingAsync(cancellationToken).ConfigureAwait(false);
            return connection.Connected
                ? HealthCheckResult.Healthy("Postgres is reachable.")
                : HealthCheckResult.Unhealthy("Postgres is unreachable.");
        }
    }
}
