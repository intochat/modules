namespace IntoChat.OS.Modules.Postgres.Tests.Unit;

public sealed class PostgresCellsFacts
{
    [Fact]
    public void NullIsJsonNull()
        => Assert.Equal("null", PostgresCells.ToCell(null, PostgresTypeMap.Text));

    [Fact]
    public void BooleansUseTheTextProtocolLetters()
    {
        Assert.Equal("true", PostgresCells.ToCell("t", PostgresTypeMap.Boolean));
        Assert.Equal("false", PostgresCells.ToCell("f", PostgresTypeMap.Boolean));
    }

    [Theory]
    [InlineData("42", "42")]
    [InlineData("-1.5", "-1.5")]
    [InlineData("999999999999999", "999999999999999")]
    public void SafeNumbersAreJsonNumbers(string value, string expected)
        => Assert.Equal(expected, PostgresCells.ToCell(value, PostgresTypeMap.Number));

    [Theory]
    [InlineData("9007199254740991")]
    [InlineData("0.1234567890123456789")]
    [InlineData("NaN")]
    public void NumbersThatCannotBeExactJsonNumbersStayText(string value)
        => Assert.Equal($"\"{value}\"", PostgresCells.ToCell(value, PostgresTypeMap.Number));

    [Fact]
    public void TextIsQuotedAndCutAtFourThousandCharacters()
    {
        Assert.Equal("\"a\\u0022b\"", PostgresCells.ToCell("a\"b", PostgresTypeMap.Text));
        Assert.Equal(4002, PostgresCells.ToCell(new string('x', 5000), PostgresTypeMap.Text).Length);
    }
}
