namespace IntoChat.OS.Modules.ClickHouse.Tests.Unit;

public sealed class ClickHouseQueryGuardFacts
{
    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("  select a, b from t where a = 'x; y' limit 5")]
    [InlineData("WITH q AS (SELECT 1 AS n) SELECT n FROM q")]
    [InlineData("SELECT format('{}', a) FROM t")]
    public void ASingleReadOnlySelectIsAccepted(string sql)
        => ClickHouseQueryGuard.Validate(sql);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("INSERT INTO t VALUES (1)")]
    [InlineData("SELECT 1; SELECT 2")]
    [InlineData("SELECT 1 -- trailing")]
    [InlineData("SELECT 1 /* c */")]
    [InlineData("SELECT 1 FORMAT JSON")]
    [InlineData("SELECT 1 SETTINGS max_threads = 1")]
    [InlineData("SELECT 1 INTO OUTFILE 'x'")]
    [InlineData("SELECT * FROM url('http://example.test', CSV)")]
    [InlineData("SELECT * FROM s3Cluster('c', 'u')")]
    [InlineData("SELECT 'unterminated")]
    public void WritesCommentsSecondStatementsAndExternalTableFunctionsAreRejected(string sql)
        => Assert.Throws<ArgumentException>(() => ClickHouseQueryGuard.Validate(sql));

    [Fact]
    public void TextInsideLiteralsNeverTripsTheTokenChecks()
    {
        ClickHouseQueryGuard.Validate("SELECT 'DROP TABLE t', \"INSERT\" FROM t");
        Assert.Equal("SELECT '?', \"?\" FROM t", ClickHouseQueryGuard.MaskQuoted("SELECT 'DROP TABLE t', \"INSERT\" FROM t"));
    }

    [Fact]
    public void AnOverlongQueryIsRejectedWithTheLengthInTheMessage()
    {
        var error = Assert.Throws<ArgumentException>(() => ClickHouseQueryGuard.Validate("SELECT " + new string('1', 20_001)));
        Assert.Contains("20000", error.Message, StringComparison.Ordinal);
    }
}
