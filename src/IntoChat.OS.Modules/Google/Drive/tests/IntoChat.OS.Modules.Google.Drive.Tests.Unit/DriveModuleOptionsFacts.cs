using IntoChat.OS.Framework.Modules.Validation;

namespace IntoChat.OS.Modules.Google.Drive.Tests.Unit;

public sealed class DriveModuleOptionsFacts
{
    [Theory]
    [InlineData("http://provider.example/drive/")]
    [InlineData("https://provider.example/drive")]
    [InlineData("https://name:secret@provider.example/drive/")]
    [InlineData("https://provider.example/drive/?token=private")]
    public void UnsafeEndpointsAreRejectedWithoutEchoingTheirValue(string endpoint)
    {
        var error = Assert.Throws<ModuleValidationException>(() => new DriveModuleOptions { Endpoint = new(endpoint) }.Validate());
        Assert.Contains(error.Issues, issue => issue.Path == "Endpoint");
        Assert.DoesNotContain(endpoint, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NewConnectionsStayReadOnlyUntilTheWriteScopeIsVerified()
    {
        new DriveModuleOptions().Validate();
        new DriveModuleOptions { Endpoint = new("http://127.0.0.1:49152/drive/") }.Validate();
        Assert.Equal("https://www.googleapis.com/auth/drive.readonly", DriveScopes.Read);
        Assert.Equal([DriveScopes.Read], Assert.Single(DriveModule.Connector.OAuthMethods).Scopes);
        Assert.Equal("google-drive", DriveModule.Connector.Id);
        Assert.False(DriveScopes.GrantsWrite(DriveScopes.Read));
        Assert.True(DriveScopes.GrantsWrite(DriveScopes.Read + " " + DriveScopes.Write));
        Assert.True(DriveScopes.GrantsWrite(DriveScopes.Files));
    }
}
