namespace IntoChat.OS.Modules.ClickHouse.Tests.Unit;

public sealed class ClickHouseTypeMapFacts
{
    [Theory]
    [InlineData("Int32", "number")]
    [InlineData("UInt64", "number")]
    [InlineData("Float64", "number")]
    [InlineData("Decimal(18, 4)", "number")]
    [InlineData("Int128", "text")]
    [InlineData("Bool", "boolean")]
    [InlineData("Date", "date")]
    [InlineData("Date32", "date")]
    [InlineData("DateTime", "text")]
    [InlineData("Array(Int32)", "text")]
    [InlineData("Nullable(Int64)", "number")]
    [InlineData("LowCardinality(Nullable(String))", "text")]
    public void ColumnTypesCollapseOntoTheFourTableTypes(string clickHouseType, string expected)
        => Assert.Equal(expected, ClickHouseTypeMap.ToTableType(clickHouseType));

    [Fact]
    public void OnlyWrappersAreUnwrapped()
    {
        Assert.Equal("String", ClickHouseTypeMap.Unwrap(" Nullable(LowCardinality(String)) "));
        Assert.Equal("Array(Nullable(Int8))", ClickHouseTypeMap.Unwrap("Array(Nullable(Int8))"));
    }

    [Theory]
    [InlineData("LowCardinality(String)", true)]
    [InlineData("Nullable(LowCardinality(String))", true)]
    [InlineData("Enum8('a' = 1, 'b' = 2)", true)]
    [InlineData("String", false)]
    [InlineData("Array(LowCardinality(String))", false)]
    public void CategoricalColumnsAreLowCardinalityStringsAndEnums(string clickHouseType, bool expected)
        => Assert.Equal(expected, ClickHouseTypeMap.IsCategorical(clickHouseType));

    [Fact]
    public void ABlankTypeIsRejected()
        => Assert.Throws<ArgumentException>(() => ClickHouseTypeMap.ToTableType(" "));
}
