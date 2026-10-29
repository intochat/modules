namespace IntoChat.OS.Modules.Tools;

// InputSchema is the tool's JSON schema for Run's arguments object.
public sealed record ToolDescriptor(
    string Name,
    string Title,
    string Description,
    string InputSchema,
    bool ReadOnly);

// What a source offers right now: its title and its tools; a source that is not set up offers none.
public sealed record ToolSet(
    string Title,
    ToolDescriptor[] Tools);

// Blobs carry binary content (a rendered PNG): base64 Data, or a Uri when the source stored it.
public sealed record ToolResult(
    bool IsError,
    string Text,
    ToolBlob[] Blobs);

public sealed record ToolBlob(
    string MediaType,
    string Data,
    string? Uri = null);
