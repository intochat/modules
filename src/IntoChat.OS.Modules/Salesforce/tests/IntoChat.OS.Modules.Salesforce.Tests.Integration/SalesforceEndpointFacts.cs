using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Framework.Modules.Platform.Integrations;
using IntoChat.OS.Modules.Salesforce;
using Xunit;

namespace IntoChat.OS.Modules.Salesforce.Tests.Integration;

public sealed class SalesforceEndpointFacts
{
    [Fact]
    public void TheModuleMapsNoRoutesBecauseLoginAndCallbackAreThePlatforms()
        => Assert.False(typeof(IHttpModule).IsAssignableFrom(typeof(SalesforceModule)));

    [Fact]
    public void TheOrgOAuthMethodReadsItsClientFromTheDeclaredSalesforceIntegration()
    {
        var connector = Assert.Single(SalesforceModule.Connectors);
        var method = Assert.Single(connector.OAuthMethods);
        var integration = SalesforceModule.Integration;
        Assert.Equal(integration.Id, method.IntegrationId);

        Assert.Contains(method.Id, connector.AuthMethods);
        Assert.Contains(method.IntegrationId, connector.RequiredIntegrations);
        Assert.Contains(method.ClientIdField, integration.SecretFields);
        Assert.Contains(method.ClientSecretField, integration.SecretFields);
        Assert.Equal(["mcp_api", "refresh_token"], method.Scopes);
        Assert.True(method.Pkce);
    }

    [Fact]
    public void LocalProtocolFixtureRequiresAnExplicitLoopbackChoice()
    {
        var options = new SalesforceModuleOptions { McpEndpoint = new("http://127.0.0.1:1234/mcp") };
        Assert.Throws<InvalidOperationException>(options.ResolveMcpEndpoint);
        options.AllowLoopbackMcp = true;
        Assert.Equal(options.McpEndpoint, options.ResolveMcpEndpoint());
        options.McpEndpoint = new("http://example.com/mcp");
        Assert.Throws<InvalidOperationException>(options.ResolveMcpEndpoint);
        Assert.Equal(SalesforceModule.DefaultMcpEndpoint, new SalesforceModuleOptions().ResolveMcpEndpoint());
    }
}
