namespace IntoChat.OS.Modules.Postgres;

/// <summary>
/// Opens the Postgres provider a person's connection string reaches. Hosts and tests replace it through
/// <c>ConfigureServices</c> to stand a fake database in for a real server.
/// </summary>
public interface IConnectionPostgresProviderFactory
{
    /// <summary>The provider for the given connection string.</summary>
    IPostgresProvider ForConnectionString(string connectionString);
}
