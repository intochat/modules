using System.Text.Json;

namespace IntoChat.OS.Modules.Salesforce;

internal interface ISalesforceProvider
{
    Task<JsonElement> InvokeAsync(string tool, JsonElement arguments, string accessToken, CancellationToken cancellationToken);
    Task<string> ReadToolSchemaHashAsync(string tool, string accessToken, CancellationToken cancellationToken);
}
