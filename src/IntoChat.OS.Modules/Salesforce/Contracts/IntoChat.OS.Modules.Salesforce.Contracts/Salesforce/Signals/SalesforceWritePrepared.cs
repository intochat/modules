using IntoChat.OS;

namespace IntoChat.OS.Modules.Salesforce.Signals;

[DigitalBrain.Signal("salesforce.write-prepared")]
public sealed record SalesforceWritePrepared(SalesforceWritePreview Preview) : DigitalBrain.Signal;
