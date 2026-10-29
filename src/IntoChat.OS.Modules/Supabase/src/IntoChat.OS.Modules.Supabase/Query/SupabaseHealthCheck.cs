using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Supabase;

internal sealed class SupabaseHealthCheck(ISupabaseBackingStore provider, IOptions<SupabaseModuleOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!options.Value.UseBackingStore) { return HealthCheckResult.Healthy("Supabase deployment backing storage is not selected."); }
        var connection = await provider.PingAsync(cancellationToken).ConfigureAwait(false);
        return connection.Connected
            ? HealthCheckResult.Healthy($"Supabase {connection.ServerVersion} serves database '{connection.Database}'.")
            : HealthCheckResult.Unhealthy($"Supabase database '{connection.Database}' is unreachable.");
    }
}
