using IntoChat.OS;

namespace IntoChat.OS.Modules.Salesforce.Signals;

[DigitalBrain.Signal("salesforce.write-failed")]
public sealed record SalesforceWriteFailed(string PreviewId) : DigitalBrain.Signal;
