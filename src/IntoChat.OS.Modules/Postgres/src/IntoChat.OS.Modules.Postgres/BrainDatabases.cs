using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using IntoChat.OS.Connections.Platform.Integrations;
using Npgsql;

namespace IntoChat.OS.Modules.Postgres;

// DatabasePerBrain isolation: one database per brain on the deployment's server, created over the
// AdminConnection integration secret the first time a brain places a table.
internal sealed class BrainDatabases(IIntegrationHandle integration, Func<string, string, CancellationToken, Task>? createDatabase = null)
{
    private readonly ConcurrentDictionary<string, string> _ensured = new(StringComparer.Ordinal);

    public static string DatabaseName(string brainId)
        => "brain_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(brainId)))[..16];

    public async Task<string> Ensure(string brainId, CancellationToken ct)
    {
        var database = DatabaseName(brainId);
        if (_ensured.ContainsKey(database)) { return database; }
        var admin = await integration.ReadSecretAsync("AdminConnection", ct);
        await (createDatabase ?? CreateDatabase)(admin, database, ct);
        _ensured[database] = database;
        return database;
    }

    private static async Task CreateDatabase(string adminConnection, string database, CancellationToken ct)
    {
        try
        {
            await using var source = NpgsqlDataSource.Create(PostgresConnectionSettings.Parse(adminConnection).ConnectionString);
            await using var connection = await source.OpenConnectionAsync(ct);
            await using (var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = $1", connection))
            {
                exists.Parameters.AddWithValue(database);
                if (await exists.ExecuteScalarAsync(ct) is not null) { return; }
            }
            // CREATE DATABASE cannot be parameterized; the name is hash-derived, never user input.
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
            try { await create.ExecuteNonQueryAsync(ct); }
            catch (PostgresException error) when (error.SqlState == "42P04") { return; } // created by a concurrent Ensure
        }
        catch (PostgresException) { throw; }
        catch (NpgsqlException) { throw new PostgresUnavailableException("Postgres is unreachable or the database connection failed."); }
    }
}
