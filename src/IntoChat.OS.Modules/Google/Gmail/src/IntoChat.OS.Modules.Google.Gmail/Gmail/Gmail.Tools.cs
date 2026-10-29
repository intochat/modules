using System.Text.Json;
using System.Text.Json.Serialization;
using IntoChat.OS.Modules.Tools;

namespace IntoChat.OS.Modules.Google.Gmail;

internal sealed partial class GmailNeuron
{
    private static readonly JsonSerializerOptions ToolJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false,
        MaxDepth = 16,
    };

    private static readonly ToolDescriptor InboxTool = new("gmail_read_inbox", "Read inbox",
        "Read one bounded inbox page. Continuations follow provider ordering and are not snapshots; missing messages are explicit.",
        """{"type":"object","properties":{"pageSize":{"type":"integer","minimum":1,"maximum":100,"default":25},"pageToken":{"type":["string","null"],"maxLength":8192}},"additionalProperties":false}""", true);
    private static readonly ToolDescriptor PreviewTool = new("gmail_preview_draft", "Preview draft",
        "Validate and retain a local draft preview for ten minutes. Creates no Google draft. Supply one to 100 recipients total; headers cannot contain CR or LF. Subject limit is 998 UTF-8 bytes; body limit is 256 KiB.",
        """{"type":"object","properties":{"to":{"type":"array","items":{"type":"string"},"maxItems":100},"cc":{"type":"array","items":{"type":"string"},"maxItems":100},"bcc":{"type":"array","items":{"type":"string"},"maxItems":100},"subject":{"type":"string","description":"May be empty; at most 998 UTF-8 bytes, without CR, LF or null characters."},"body":{"type":"string","description":"May be empty; at most 262144 UTF-8 bytes."}},"required":["to","cc","bcc","subject","body"],"additionalProperties":false}""", false);
    private static readonly ToolDescriptor CreateTool = new("gmail_create_draft", "Create draft",
        "Create a Google draft from a reviewed preview; never sends mail. Retain one request ID before calling and reuse it for recovery. Pending or Unknown does not authorize a new request. No automatic mutation retries.",
        """{"type":"object","properties":{"previewId":{"type":"string","minLength":1,"maxLength":128},"requestId":{"type":"string","minLength":1,"maxLength":128}},"required":["previewId","requestId"],"additionalProperties":false}""", false);
    private static readonly ToolDescriptor OperationTool = new("gmail_get_draft_operation", "Read draft operation",
        "Read the caller's retained local draft receipt, including after disconnect. Missing or expired receipts do not prove that no draft was created. Unknown requires review, not an automatic retry.",
        """{"type":"object","properties":{"requestId":{"type":"string","minLength":1,"maxLength":128}},"required":["requestId"],"additionalProperties":false}""", true);

    // Tools is interleavable: read the published availability only, without touching credentials or providers.
    private ToolDescriptor[] ToolDescriptors() => HasAcceptedMailbox
        ? [InboxTool, PreviewTool, CreateTool, OperationTool]
        : HasDraftReceipts ? [OperationTool] : [];

    public Task<ToolSet> Tools(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new ToolSet("Gmail", ToolDescriptors()));
    }

    public async Task<ToolResult> Run(string tool, string arguments, CancellationToken ct = default)
    {
        RequireCaller();
        ct.ThrowIfCancellationRequested();
        switch (tool)
        {
            case "gmail_read_inbox":
                var inbox = ParseToolRequest<GmailInboxRequest>(arguments) is { } inboxRequest
                    ? await ReadInboxCore(inboxRequest, ct) : GmailInboxResult.FromFailure(InvalidToolArguments());
                return ToolResponse(inbox, inbox.Failure is not null);
            case "gmail_preview_draft":
                var preview = ParseToolRequest<GmailDraftRequest>(arguments) is { } previewRequest
                    ? await PreviewDraftCore(previewRequest, ct) : GmailDraftPreviewResult.FromFailure(InvalidToolArguments());
                return ToolResponse(preview, preview.Failure is not null);
            case "gmail_create_draft":
                var created = ParseToolRequest<GmailCreateDraftRequest>(arguments) is { } createRequest
                    ? await CreateDraftCore(createRequest, ct) : GmailDraftCreationResult.FromFailure(InvalidToolArguments());
                return ToolResponse(created, created.Failure is not null || FailedOperation(created.Value));
            case "gmail_get_draft_operation":
                var operation = ParseToolRequest<GmailDraftOperationRequest>(arguments) is { } operationRequest
                    ? await GetDraftOperationCore(operationRequest, ct) : GmailDraftOperationResult.FromFailure(InvalidToolArguments());
                return ToolResponse(operation, operation.Failure is not null || FailedOperation(operation.Value));
            default:
                return ToolResponse(new GmailFailure("invalid_input", "Choose an advertised Gmail tool.", [new("tool", "unknown", "The tool is not available.")]), true);
        }
    }

    private static T? ParseToolRequest<T>(string? arguments) where T : class
    {
        // Enough room for the bounded UTF-8 body even when JSON escapes every character.
        if (arguments is null || arguments.Length > 2 * 1024 * 1024) { return null; }
        try { return JsonSerializer.Deserialize<T>(arguments, ToolJson); }
        catch (JsonException) { return null; }
    }

    private static GmailFailure InvalidToolArguments() => new("invalid_input", "Supply a JSON object matching the complete tool schema.",
        [new("request", "json", "The request contains invalid or unknown fields.")]);
    private static bool FailedOperation(GmailDraftOperation? operation) => operation?.Status is GmailDraftOperationStatus.Failed or GmailDraftOperationStatus.Unknown;
    private static ToolResult ToolResponse<T>(T result, bool error) => new(error, JsonSerializer.Serialize(result, ToolJson), []);
}
