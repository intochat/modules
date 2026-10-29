using IntoChat.OS.Modules.Postgres;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Connections.Configuration;
using IntoChat.OS.Framework.Configuration;
using IntoChat.OS.Framework.Modules.Configuration;
using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Modules.Postgres.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;

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
        services.TryAddSingleton<ITablesSlot, AppTablesSlot>();
        services.TryAddSingleton<ITablePlacements, TablePlacements>();
        services.TryAddSingleton<IConnectionTableStores, ConnectionTableStores>();
        services.AddSingleton<IIncomingGrainCallFilter, TableLifecycleGuard>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IntoChat.OS.Modules.Apps.IAppStorageHook, TableStorageHook>());
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
