namespace IntoChat.OS.Modules.Postgres.Tests.Unit;

public sealed class PostgresQueryGuardFacts
{
    [Theory]
    [InlineData("SELECT 1", "SELECT 1")]
    [InlineData("  select * from t where a = $1 ;  ", "select * from t where a = $1")]
    [InlineData("WITH q AS (SELECT 1) SELECT * FROM q", "WITH q AS (SELECT 1) SELECT * FROM q")]
    [InlineData("SELECT 'DROP TABLE t; --' AS note", "SELECT 'DROP TABLE t; --' AS note")]
    public void ASingleReadOnlySelectIsNormalised(string sql, string expected)
        => Assert.Equal(expected, PostgresQueryGuard.Normalize(sql));

    [Theory]
    [InlineData("")]
    [InlineData("DELETE FROM t")]
    [InlineData("SELECT 1; SELECT 2")]
    [InlineData("SELECT 1 -- c")]
    [InlineData("SELECT 1 /* c */")]
    [InlineData("SELECT * INTO t2 FROM t")]
    [InlineData("SELECT set_config('a', 'b', false)")]
    [InlineData("SELECT \"pg_sleep\"(5)")]
    [InlineData("SELECT pg_read_file('x')")]
    [InlineData("SELECT $$x$$")]
    [InlineData("SELECT E'\\x'")]
    [InlineData("SELECT 'unterminated")]
    public void WritesSessionControlCommentsAndSeparatorsAreRejected(string sql)
        => Assert.Throws<ArgumentException>(() => PostgresQueryGuard.Normalize(sql));

    [Fact]
    public void MaskingKeepsIdentifiersVisibleAndHidesLiteralText()
        => Assert.Equal("SELECT  ?  FROM  col ", PostgresQueryGuard.MaskQuoted("SELECT 'DROP' FROM \"col\""));

    [Fact]
    public void AnOverlongQueryIsRejectedWithTheReason()
    {
        var error = Assert.Throws<ArgumentException>(() => PostgresQueryGuard.Normalize("SELECT " + new string('1', 20_001)));
        Assert.StartsWith(PostgresQueryGuard.Reason, error.Message, StringComparison.Ordinal);
    }
}
