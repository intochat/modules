using DigitalBrain;

namespace IntoChat.OS.Modules.Postgres.Tables.Signals;

[Signal("postgres.table.defined")]
public sealed record TableDefined(string Table, long Revision) : DigitalBrain.Signal;
