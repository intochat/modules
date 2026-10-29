using IntoChat.OS.Framework.Modules.Validation;

namespace IntoChat.OS.Modules.Google.Gmail.Tests.Unit;

public sealed class GmailModuleOptionsFacts
{
    [Theory]
    [InlineData("http://provider.example/profile")]
    [InlineData("https://name:secret@provider.example/profile")]
    [InlineData("https://provider.example/profile?token=private")]
    [InlineData("https://provider.example/profile#fragment")]
    public void UnsafeProfileEndpointsAreRejectedWithoutEchoingTheirValue(string endpoint)
    {
        var error = Assert.Throws<ModuleValidationException>(() => new GmailModuleOptions { ProfileEndpoint = new(endpoint) }.Validate());
        Assert.Contains(error.Issues, issue => issue.Path == "ProfileEndpoint");
        Assert.DoesNotContain(endpoint, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionAndLoopbackEndpointsAreAccepted()
    {
        new GmailModuleOptions().Validate();
        new GmailModuleOptions { ProfileEndpoint = new("http://127.0.0.1:49152/profile") }.Validate();
    }
}
