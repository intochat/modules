namespace IntoChat.OS.Modules.Postgres.Tests.Unit;

public sealed class PostgresTypeMapFacts
{
    [Theory]
    [InlineData("bool", "boolean")]
    [InlineData("int4", "number")]
    [InlineData("bigint", "number")]
    [InlineData("double precision", "number")]
    [InlineData("numeric", "number")]
    [InlineData("money", "number")]
    [InlineData("uuid", "text")]
    [InlineData("jsonb", "text")]
    [InlineData("timestamptz", "text")]
    public void DatabaseTypesCollapseOntoTheTableTypes(string type, string expected)
        => Assert.Equal(expected, PostgresTypeMap.ToTableType(type));

    [Theory]
    [InlineData("text")]
    [InlineData("number")]
    [InlineData("date")]
    [InlineData("boolean")]
    public void ATableTypeMapsToItself(string type)
        => Assert.Equal(type, PostgresTypeMap.ToTableType(type));
}
