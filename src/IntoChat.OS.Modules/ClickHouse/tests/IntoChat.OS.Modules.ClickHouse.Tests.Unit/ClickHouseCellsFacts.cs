using System.Text.Json;

namespace IntoChat.OS.Modules.ClickHouse.Tests.Unit;

public sealed class ClickHouseCellsFacts
{
    [Fact]
    public void NullAndDbNullBecomeJsonNull()
    {
        Assert.Equal(JsonValueKind.Null, ClickHouseCells.ToCell(null, ClickHouseTypeMap.Text).ValueKind);
        Assert.Equal(JsonValueKind.Null, ClickHouseCells.ToCell(DBNull.Value, ClickHouseTypeMap.Number).ValueKind);
    }

    [Fact]
    public void SafeNumbersStayNumbers()
    {
        Assert.Equal(42, ClickHouseCells.ToCell(42, ClickHouseTypeMap.Number).GetInt32());
        Assert.Equal(1.5m, ClickHouseCells.ToCell(1.5m, ClickHouseTypeMap.Number).GetDecimal());
        Assert.Equal(4.6, ClickHouseCells.ToCell(4.6f, ClickHouseTypeMap.Number).GetDouble());
    }

    [Fact]
    public void NumbersBeyondTheSafeRangeFallBackToText()
    {
        var big = ClickHouseCells.ToCell(ulong.MaxValue, ClickHouseTypeMap.Number);
        Assert.Equal(JsonValueKind.String, big.ValueKind);
        Assert.Equal("18446744073709551615", big.GetString());
        Assert.Equal(JsonValueKind.String, ClickHouseCells.ToCell(0.1234567890123456789m, ClickHouseTypeMap.Number).ValueKind);
        Assert.Equal(JsonValueKind.String, ClickHouseCells.ToCell(double.NaN, ClickHouseTypeMap.Number).ValueKind);
    }

    [Fact]
    public void DatesAreDayStamps()
    {
        Assert.Equal("2026-10-18", ClickHouseCells.ToCell(new DateOnly(2026, 10, 18), ClickHouseTypeMap.Date).GetString());
        Assert.Equal("2026-10-18", ClickHouseCells.ToCell(new DateTime(2026, 10, 18, 9, 30, 0, DateTimeKind.Utc), ClickHouseTypeMap.Date).GetString());
    }

    [Fact]
    public void DateTimeTextFollowsTheColumnScale()
    {
        var moment = new DateTime(2026, 10, 18, 9, 30, 15, 123, DateTimeKind.Utc);
        Assert.Equal("2026-10-18 09:30:15", ClickHouseCells.ToCell(moment, ClickHouseTypeMap.Text, "DateTime").GetString());
        Assert.Equal("2026-10-18 09:30:15.123", ClickHouseCells.ToCell(moment, ClickHouseTypeMap.Text, "DateTime64(3)").GetString());
    }

    [Fact]
    public void StructuredValuesAreJsonAndLongTextIsCut()
    {
        Assert.Equal("[1,2,3]", ClickHouseCells.ToCell(new[] { 1, 2, 3 }, ClickHouseTypeMap.Text).GetString());
        Assert.Equal("{\"a\":1}", ClickHouseCells.ToCell(new Dictionary<string, int> { ["a"] = 1 }, ClickHouseTypeMap.Text).GetString());
        Assert.Equal(ClickHouseCells.MaxTextLength, ClickHouseCells.ToCell(new string('x', 5000), ClickHouseTypeMap.Text).GetString()!.Length);
    }

    [Fact]
    public void BooleanColumnsKeepBooleans()
        => Assert.True(ClickHouseCells.ToCell(true, ClickHouseTypeMap.Boolean).GetBoolean());
}
