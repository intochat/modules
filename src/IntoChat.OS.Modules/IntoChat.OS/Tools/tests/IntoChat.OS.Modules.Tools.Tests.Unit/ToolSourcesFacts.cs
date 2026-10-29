namespace IntoChat.OS.Modules.Tools.Tests.Unit;

public sealed class ToolSourcesFacts
{
    [Fact]
    public void ACapabilityIsKeyedByItsSourceAndClipsItsDescription()
    {
        var entry = ToolCapabilitySource.Capability("b/mcp/photocraft", "Test.IFake", "Photo", new("render_preview", "Render", new string('d', 1500), "{\"type\":\"object\"}", false));

        Assert.Equal(("tool:b/mcp/photocraft/render_preview", "b/mcp/photocraft", "render_preview", "Test.IFake"), (entry.Id, entry.Key, entry.Tool, entry.Contract));
        Assert.Equal(ToolCapabilitySource.MaxDescription, entry.Description.Length);
        Assert.Contains("Run(\"render_preview\"", Assert.Single(entry.Methods), StringComparison.Ordinal);
    }

    [Fact]
    public void SearchDirectsTheCallerToCompleteSchemasInsteadOfClippingJson()
    {
        var schema = "{\"type\":\"object\",\"description\":\"" + new string('x', 900) + "\"}";
        var entry = ToolCapabilitySource.Capability("b/gmail/mail", "Test.IGmail", "Mail", new("gmail_create_draft", "Create draft", "Create a draft.", schema, false));

        var method = Assert.Single(entry.Methods);
        Assert.Contains("Tools()", method, StringComparison.Ordinal);
        Assert.Contains("full input schema", method, StringComparison.Ordinal);
        Assert.DoesNotContain("{\"type\"", method, StringComparison.Ordinal);
        Assert.Contains("Run(\"gmail_create_draft\", arguments)", method, StringComparison.Ordinal);
    }
}
