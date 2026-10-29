using IntoChat.OS;

namespace IntoChat.OS.Modules.Salesforce;

// One Salesforce org connection, keyed {brain}/connection/salesforce/{id}. Connecting, probing and
// disconnecting it are IConnection's; reads may refresh the token and persist it, so none is read-only.
[DigitalBrain.Contracts.NeuronAlias("salesforce"), DigitalBrain.Contracts.DefaultNeuronType("salesforce")]
public interface ISalesforce
{
    Task<SalesforceWritePreview> PrepareWrite(PrepareSalesforceWrite command);

    Task<SalesforceWritePreview> ConfirmWrite(ConfirmSalesforceWrite command);

    Task<SalesforceQueryResult> Query(SoqlQuery query, CancellationToken cancellationToken = default);

    Task<SalesforceSchema> ReadSchema(ReadSalesforceSchema query, CancellationToken cancellationToken = default);

    Task<SalesforceUserInfo> ReadUserInfo(CancellationToken cancellationToken = default);
}
