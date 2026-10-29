using System.Text.Json.Serialization;
using IntoChat.OS.Modules.AI.Models;

namespace IntoChat.OS.Modules.AI.Inference;

public sealed record AiMessage(string Role, IReadOnlyList<AiContent> Content)
{
    public IReadOnlyList<AiContent> Content
    {
        get;
        init => field = value.ToArray();
    } = Content.ToArray();
}
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$content")]
[JsonDerivedType(typeof(AiText), "text")]
[JsonDerivedType(typeof(AiReasoning), "reasoning")]
[JsonDerivedType(typeof(AiImage), "image")]
[JsonDerivedType(typeof(AiAudio), "audio")]
[JsonDerivedType(typeof(AiToolCall), "tool-call")]
[JsonDerivedType(typeof(AiToolResult), "tool-result")]
public abstract record AiContent;
public sealed record AiText(string Text) : AiContent;
public sealed record AiReasoning(string Text) : AiContent;
public sealed record AiImage(string Uri, string? MediaType = null) : AiContent;
public sealed record AiAudio(string Uri, string? MediaType = null) : AiContent;
public sealed record AiToolCall(string CallId, string Name, string ArgumentsJson) : AiContent;
public sealed record AiToolResult(string CallId, string ResultJson) : AiContent;
public sealed record InferenceTool(string Name, string Description, string ParametersJson);
public sealed record InferenceOptions(
    float? Temperature = null,
    float? TopP = null,
    int? MaxOutputTokens = null,
    string? Reasoning = null,
    Dictionary<string, string>? ProviderOptions = null,
    string? ResponseSchemaJson = null,
    ProviderInferenceOptions? Provider = null);
public sealed record InferenceRequest(
    IReadOnlyList<AiMessage> Messages,
    ModelSelection? Model = null,
    InferenceOptions? Options = null,
    IReadOnlyList<InferenceTool>? Tools = null);
public sealed record InferenceUsage(long? InputTokens, long? OutputTokens, long? TotalTokens);
public sealed record InferenceResult(
    IReadOnlyList<AiMessage> Messages,
    string? FinishReason,
    InferenceUsage? Usage,
    string? ResponseId,
    string Model);
public sealed record InferenceUpdate(
    string? Role,
    IReadOnlyList<AiContent> Content,
    string? FinishReason,
    InferenceUsage? Usage,
    string? ResponseId,
    string? MessageId = null,
    string? Model = null);

public enum CapabilitySupport { Unknown, Supported, Unsupported }
public sealed record ModelDescriptor(
    string Provider,
    string Model,
    string? Profile,
    string Revision,
    CapabilitySupport Tools,
    CapabilitySupport Vision,
    CapabilitySupport StructuredOutput,
    long? ContextWindowTokens = null,
    long? MaximumOutputTokens = null,
    CapabilitySupport Temperature = CapabilitySupport.Unknown,
    CapabilitySupport TopP = CapabilitySupport.Unknown,
    IReadOnlyList<string>? AllowedReasoning = null);
