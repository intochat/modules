using IntoChat.OS;

namespace IntoChat.OS.Modules.Salesforce.Signals;

[DigitalBrain.Signal("salesforce.write-uncertain")]
public sealed record SalesforceWriteUncertain(string PreviewId) : DigitalBrain.Signal;
