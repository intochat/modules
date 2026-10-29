using DigitalBrain;

namespace IntoChat.OS.Modules.Postgres.Tables.Signals;

[Signal("postgres.table.deleted")]
public sealed record RowDeleted(string Table, TableValue[] Key) : DigitalBrain.Signal;
