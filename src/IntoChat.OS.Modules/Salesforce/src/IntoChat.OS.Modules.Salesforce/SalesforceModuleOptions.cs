using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Salesforce;

public sealed record SalesforceModuleOptions : IModuleOptions
{
    public Uri? McpEndpoint { get; set; }
    public bool HostMcp { get; set; }

    // A loopback endpoint is a local protocol fixture; it must be chosen explicitly.
    public bool AllowLoopbackMcp { get; set; }

    public SalesforceModuleOptions WithHostedMcp(Uri? endpoint = null)
    {
        HostMcp = true;
        McpEndpoint = endpoint;
        AllowLoopbackMcp = false;
        return this;
    }

    public SalesforceModuleOptions WithLocalMcp(Uri endpoint)
    {
        HostMcp = true;
        McpEndpoint = endpoint;
        AllowLoopbackMcp = true;
        return this;
    }

    public void Validate()
    {
        if (McpEndpoint is { IsAbsoluteUri: false })
        { throw new ArgumentException("Salesforce endpoints must be absolute."); }
        if (AllowLoopbackMcp && (McpEndpoint is not { IsLoopback: true } local || local.Scheme is not ("http" or "https")))
        { throw new ArgumentException("A local MCP endpoint must use loopback HTTP(S)."); }
    }

    internal Uri ResolveMcpEndpoint()
    {
        var uri = McpEndpoint ?? SalesforceModule.DefaultMcpEndpoint;
        if (AllowLoopbackMcp && uri.IsAbsoluteUri && uri.IsLoopback
            && uri.Scheme is "http" or "https" && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0)
        {
            return uri;
        }
        if (!uri.IsAbsoluteUri
            || uri.Scheme != Uri.UriSchemeHttps || uri.Host != "api.salesforce.com" || !uri.IsDefaultPort
            || !uri.AbsolutePath.StartsWith("/platform/mcp/", StringComparison.Ordinal)
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
        {
            throw new InvalidOperationException(
                "The Salesforce MCP endpoint must be an HTTPS hosted MCP endpoint on api.salesforce.com.");
        }

        return uri;
    }
}
