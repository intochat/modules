using IntoChat.OS.Connections.Platform.Connections;

namespace IntoChat.OS.Modules.Salesforce;

// The org's instance URL is the connection's Account.
[GenerateSerializer, Alias("salesforce.state")]
internal sealed class SalesforceState : ConnectionState
{
    [Id(8)] public SalesforceWritePreview? PendingWrite { get; set; }
    [Id(9)] public SalesforceWritePreview? SubmittingWrite { get; set; }
}
