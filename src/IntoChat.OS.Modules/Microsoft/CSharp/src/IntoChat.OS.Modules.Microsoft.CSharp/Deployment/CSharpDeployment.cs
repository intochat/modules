using System.Security.Cryptography;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework.Modules.Configuration;
using IntoChat.OS.Modules.Microsoft.Azure;

namespace IntoChat.OS.Modules.Microsoft.CSharp;

// Behaviors run in a custom-container session pool: one Hyper-V session per brain running the Sandbox
// image, every behavior of that brain loaded into it. The pool has its own network, environment and
// subnet, whose NSG lets sessions out on HTTPS only (the runtime edge and assembly downloads); limiting
// that to those alone needs Azure Firewall rules.
public sealed class CSharpDeployment : ModuleDeployment<CSharpModule>
{
    public const string RunTokenKeySecret = "csharp-run-token-key";

    private const string AcrPull = "7f951dda-4ed3-4680-a7ca-43fe172d538d";

    // Settings: "sandboxImage" (the Sandbox image in the registry), "sandboxSessions" (how many brains can
    // have a live session at once, default 100), "sessionExecutorRoleId" (the id of the built-in
    // "Azure ContainerApps Session Executor" role:
    // az role definition list --name "Azure ContainerApps Session Executor" --query "[0].name" -o tsv),
    // "sandboxSubnet" (default 10.0.10.0/23 in "sandboxNetwork", default 10.0.0.0/16), "registryId" (the
    // registry resource id, when set the runtime identity gets AcrPull on it) and "edgeUrl" (the OS's
    // public URL, which sessions dial back to).
    public override async Task Deploy(ModuleDeploymentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var name = context.Name("sandbox");
        var network = name + "-vnet";
        var identity = context.Foundation.Identity.Name;

        await context.Ensure(new NetworkSecurityGroupSpec
        {
            Name = name,
            Rules =
            [
                new SecurityRuleSpec { Name = "allow-https-out", Priority = 100, Access = "Allow", Protocol = "Tcp", DestinationAddressPrefix = "*", DestinationPortRange = "443" },
                new SecurityRuleSpec { Name = "deny-other-out", Priority = 4000, Access = "Deny", Protocol = "*", DestinationAddressPrefix = "Internet", DestinationPortRange = "*" },
            ],
        });
        await context.Ensure(new VirtualNetworkSpec { Name = network, AddressSpace = context.Settings.Get("sandboxNetwork") ?? "10.0.0.0/16" });
        var subnet = await context.Ensure(new SubnetSpec
        {
            Name = name,
            VirtualNetwork = network,
            AddressPrefix = context.Settings.Get("sandboxSubnet") ?? "10.0.10.0/23",
            NetworkSecurityGroup = name,
            DelegationService = "Microsoft.App/environments",
        });
        await context.Ensure(new ManagedEnvironmentSpec { Name = name, LogWorkspace = context.Name("log"), SubnetId = subnet.Id });

        if (context.Settings.Get("registryId") is { } registry) { await context.Grant("sandbox-registry-pull", AcrPull, registry); }
        // The pool pulls the image as the runtime identity; sessions never see it.
        var pool = await context.Ensure(new SessionPoolSpec
        {
            Name = name,
            Environment = name,
            Image = context.Settings.Require("sandboxImage"),
            Identity = identity,
            RegistryServer = context.Foundation.RegistryServer,
            MaxSessions = context.Settings.GetInt32("sandboxSessions") ?? 100,
        });
        await context.Grant("sandbox-sessions", context.Settings.Require("sessionExecutorRoleId"), pool.Id);

        // Session tokens must verify on every silo, so the key is generated once and kept in Key Vault: a
        // later install never replaces it.
        await context.Ensure(new SecretSpec
        {
            Name = RunTokenKeySecret,
            Vault = context.Foundation.KeyVault.Name,
            Value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            Overwrite = false,
        });
        context.AddRuntimeSecret(new SecretRef
        {
            Name = RunTokenKeySecret,
            Vault = context.Foundation.KeyVault.Name,
            VaultSecret = RunTokenKeySecret,
            Variable = ConfigurationKeyGrammar.EnvironmentName(ConfigurationKeyGrammar.IntegrationKey(CSharpModule.Integration.Id, "RunTokenKey")),
        });
        if (!context.IsDryRun)
        {
            context.SetRuntimeVariable(
                IntoChatOSConfiguration.EnvironmentName(CSharpOptions.SectionName, nameof(CSharpOptions.SessionPoolEndpoint)),
                PoolEndpoint(context.Target, pool));
        }
        if (context.Settings.Get("edgeUrl") is { } edge)
        {
            context.SetRuntimeVariable(
                IntoChatOSConfiguration.EnvironmentName(CSharpOptions.SectionName, nameof(CSharpOptions.EdgeUrl)), edge);
        }
    }

    // Azure chooses the endpoint's environment hostname; it cannot be derived from the resource id.
    public static string PoolEndpoint(DeploymentTarget target, ResourceRef pool)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(pool);
        return !string.IsNullOrWhiteSpace(pool.Endpoint)
            ? pool.Endpoint
            : throw new InvalidOperationException($"Session pool '{pool.Name}' has no management endpoint.");
    }
}
