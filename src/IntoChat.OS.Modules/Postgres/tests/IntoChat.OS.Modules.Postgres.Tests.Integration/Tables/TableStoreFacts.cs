using IntoChat.OS.Modules.Postgres.Tables;
using Npgsql;

namespace IntoChat.OS.Modules.Postgres.Tests.Integration.Tables;

public sealed class TableStoreFacts
{
    [Theory]
    [InlineData("28P01")] // the stored password is rejected
    [InlineData("3D000")] // the pinned database no longer exists
    [InlineData("08006")] // the connection failed
    [InlineData("57P03")] // the server is starting or shutting down
    public void AFailureToReachThePlacementIsServiceUnavailable(string sqlState)
        => Assert.IsType<PostgresUnavailableException>(TableStore.Refusal(new PostgresException("refused", "FATAL", "FATAL", sqlState)));

    [Fact]
    public void AStatementPostgresRefusesStaysAQueryRefusal()
        => Assert.IsType<PostgresQueryException>(TableStore.Refusal(new PostgresException("duplicate", "ERROR", "ERROR", "23505")));
}
