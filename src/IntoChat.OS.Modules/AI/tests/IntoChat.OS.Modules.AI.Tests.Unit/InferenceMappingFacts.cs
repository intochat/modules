using IntoChat.OS.Modules.AI.Inference;
using IntoChat.OS.Modules.AI.Models;
using Microsoft.Extensions.AI;

namespace IntoChat.OS.Modules.AI.Tests.Unit;

public sealed class InferenceMappingFacts
{
    private static ModelDescriptor Model(CapabilitySupport tools = CapabilitySupport.Unsupported, CapabilitySupport vision = CapabilitySupport.Unsupported,
        CapabilitySupport temperature = CapabilitySupport.Unsupported, long? maximumOutput = null, string[]? reasoning = null)
        => new("test", "model", null, "1", tools, vision, CapabilitySupport.Unsupported,
            MaximumOutputTokens: maximumOutput, Temperature: temperature, AllowedReasoning: reasoning);

    private static InferenceRequest Request(AiContent content, InferenceOptions? options = null, IReadOnlyList<InferenceTool>? tools = null)
        => new([new AiMessage("user", [content])], Options: options, Tools: tools);

    [Fact]
    public void TextAndReasoningRoundTripThroughTheProviderMessage()
    {
        var message = new AiMessage("assistant", [new AiText("hi"), new AiReasoning("because")]);

        var back = InferenceMapping.FromChatMessage(InferenceMapping.ToChatMessage(message));

        Assert.Equal("assistant", back.Role);
        Assert.Equal<AiContent>([new AiText("hi"), new AiReasoning("because")], back.Content);
    }

    [Fact]
    public void ToolCallsAndResultsRoundTripAsJson()
    {
        var back = InferenceMapping.FromChatMessage(InferenceMapping.ToChatMessage(
            new AiMessage("assistant", [new AiToolCall("c1", "search", "{\"q\":\"x\"}"), new AiToolResult("c1", "{\"ok\":true}")])));

        var call = Assert.IsType<AiToolCall>(back.Content[0]);
        Assert.Equal(("c1", "search"), (call.CallId, call.Name));
        Assert.Contains("\"q\"", call.ArgumentsJson, StringComparison.Ordinal);
        Assert.Contains("true", Assert.IsType<AiToolResult>(back.Content[1]).ResultJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnUnknownRoleAndANullMessageAreRefused()
    {
        Assert.Throws<ArgumentException>(() => InferenceMapping.ToChatMessage(new AiMessage("narrator", [new AiText("x")])));
        Assert.Throws<ArgumentNullException>(() => InferenceMapping.ToChatMessage(null!));
    }

    [Fact]
    public void ToolArgumentsMustBeAJsonObject()
        => Assert.ThrowsAny<Exception>(() => InferenceMapping.ToContent(new AiToolCall("c", "t", "null")));

    [Theory]
    [InlineData("https://example.test/a.png", "image/png", true)]
    [InlineData("http://example.test/a.png", "image/png", true)]
    [InlineData("ftp://example.test/a.png", "image/png", false)]
    [InlineData("https://example.test/a.png", "audio/wav", false)]
    [InlineData("https://example.test/a.png", null, false)]
    [InlineData("not a uri", "image/png", false)]
    public void RemoteMediaNeedsAnHttpUriAndAMatchingMediaType(string uri, string? mediaType, bool accepted)
    {
        if (accepted) { Assert.IsType<UriContent>(InferenceMapping.ToContent(new AiImage(uri, mediaType))); }
        else { Assert.Throws<ArgumentException>(() => InferenceMapping.ToContent(new AiImage(uri, mediaType))); }
    }

    [Fact]
    public void InlineMediaMustMatchItsModalityAndFitTheBudget()
    {
        var png = "data:image/png;base64," + Convert.ToBase64String([1, 2, 3]);

        Assert.IsType<DataContent>(InferenceMapping.ToContent(new AiImage(png)));
        Assert.Throws<ArgumentException>(() => InferenceMapping.ToContent(new AiAudio(png)));
        Assert.Throws<ArgumentException>(() => InferenceMapping.ToContent(new AiImage("data:image/png;base64,")));
        Assert.Throws<ArgumentException>(() => InferenceMapping.ToContent(new AiImage("data:image/png;base64," + new string('A', 12 * 1024 * 1024))));
    }

    [Fact]
    public void ARequestNeedsMessagesAndOnlyUsesWhatTheModelDeclares()
    {
        Assert.Throws<ArgumentException>(() => InferenceMapping.ValidateRequest(new InferenceRequest([]), Model()));
        Assert.Throws<ArgumentException>(() => InferenceMapping.ValidateRequest(Request(new AiImage("https://example.test/a.png", "image/png")), Model()));
        Assert.Throws<ArgumentException>(() => InferenceMapping.ValidateRequest(Request(new AiAudio("https://example.test/a.wav", "audio/wav")), Model(vision: CapabilitySupport.Supported)));
        Assert.Throws<ArgumentException>(() => InferenceMapping.ValidateRequest(Request(new AiText("x"), tools: [new InferenceTool("t", "d", "{}")]), Model()));

        InferenceMapping.ValidateRequest(Request(new AiImage("https://example.test/a.png", "image/png")), Model(vision: CapabilitySupport.Supported));
        InferenceMapping.ValidateRequest(Request(new AiText("x"), tools: [new InferenceTool("t", "d", "{}")]), Model(tools: CapabilitySupport.Supported));
    }

    [Fact]
    public void OptionsMustBeDeclaredByTheModelProfile()
    {
        Assert.Throws<ArgumentException>(() => InferenceMapping.ValidateRequest(Request(new AiText("x"), new InferenceOptions(Temperature: 0.5f)), Model()));
        Assert.Throws<ArgumentException>(() => InferenceMapping.ValidateRequest(Request(new AiText("x"), new InferenceOptions(Reasoning: "high")), Model()));
        Assert.Throws<ArgumentException>(() => InferenceMapping.ValidateRequest(Request(new AiText("x"), new InferenceOptions(MaxOutputTokens: 100)), Model(maximumOutput: 50)));

        InferenceMapping.ValidateRequest(Request(new AiText("x"), new InferenceOptions(Temperature: 0.5f, Reasoning: "HIGH", MaxOutputTokens: 50)),
            Model(temperature: CapabilitySupport.Supported, maximumOutput: 50, reasoning: ["high"]));
    }
}
