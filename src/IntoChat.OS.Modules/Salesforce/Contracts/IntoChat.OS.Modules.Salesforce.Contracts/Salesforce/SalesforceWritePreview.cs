namespace IntoChat.OS.Modules.Salesforce;

public sealed record SalesforceWritePreview(
    string PreviewId,
    string Tool,
    string ToolSchemaHash,
    string Arguments,
    DateTimeOffset ExpiresAt,
    string? RecordId = null);
