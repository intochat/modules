using DigitalBrain;

namespace IntoChat.OS.Modules.Postgres.Tables.Signals;

[Signal("postgres.table.upserted")]
public sealed record RowUpserted(string Table, TableValue[] Key) : DigitalBrain.Signal;
