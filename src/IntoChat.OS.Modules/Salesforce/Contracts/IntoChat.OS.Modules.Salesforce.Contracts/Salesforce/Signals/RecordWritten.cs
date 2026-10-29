using IntoChat.OS;

namespace IntoChat.OS.Modules.Salesforce.Signals;

[DigitalBrain.Signal("salesforce.record-written")]
public sealed record RecordWritten(SalesforceWritePreview Preview) : DigitalBrain.Signal;
