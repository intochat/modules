namespace IntoChat.OS.Modules.ClickHouse.Query;

// TableType is the ui table column domain: text, number, date or boolean. SampleValues is filled
// by schema reads for low-cardinality and enum columns so the agent filters on real values.
public sealed record ClickHouseColumn(
    string Name,
    string ClickHouseType,
    string TableType,
    IReadOnlyList<string>? SampleValues = null);
