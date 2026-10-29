using IntoChat.OS.Connections.Platform.Integrations;
using IntoChat.OS.Framework.Modules.Configuration;
using IntoChat.OS.Framework.Modules.Platform.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntoChat.OS.Modules.Salesforce;

// An org connection is the Salesforce neuron's own sealed state; the Platform runs its browser login,
// the code exchange and the refresh through the OAuth method below.
[DigitalBrain.Module("salesforce")]
public sealed class SalesforceModule : DigitalBrain.Module
{
    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [],
        Settings: [DigitalBrain.Setting.Of<SalesforceModuleOptions>(IntoChatOSConfiguration.ModuleOptionsSection("salesforce"))],
        Secrets: []);

    public static System.Reflection.Assembly? Contracts => typeof(ISalesforce).Assembly;

    public static IntegrationSchema Integration { get; } = IntegrationSchema.For("salesforce", "Salesforce")
        .RequiresSecret("ConsumerKey")
        .RequiresSecret("ConsumerSecret");

    public static ConnectorDefinition[] Connectors => [new("salesforce", "Salesforce", ["org-oauth"], ["salesforce"])
    {
        Neurons = [typeof(ISalesforce)],
        Signals = [typeof(Signals.SalesforceWritePrepared), typeof(Signals.SalesforceWriteFailed), typeof(Signals.SalesforceWriteUncertain), typeof(Signals.RecordWritten)],
        OAuthMethods = [new("org-oauth", "https://login.salesforce.com/services/oauth2/authorize", "https://login.salesforce.com/services/oauth2/token",
            ["mcp_api", "refresh_token"], "salesforce", "ConsumerKey", "ConsumerSecret")],
    }];

    public static readonly Uri DefaultMcpEndpoint = new("https://api.salesforce.com/platform/mcp/v1/platform/sobject-all");

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton(TimeProvider.System);
        var options = configuration.GetModuleOptions<SalesforceModuleOptions>("salesforce");
        services.TryAddSingleton(options);
        // Resolved at composition, so a host with a misconfigured MCP endpoint fails to start.
        var endpoint = options.ResolveMcpEndpoint();
        services.TryAddSingleton<ISalesforceProvider>(_ => new SalesforceMcpProvider(endpoint));
    }
}
