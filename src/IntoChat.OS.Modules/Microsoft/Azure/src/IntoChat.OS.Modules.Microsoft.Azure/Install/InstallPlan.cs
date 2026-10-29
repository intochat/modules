using System.Globalization;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Connections.Configuration;
using IntoChat.OS.Framework.Configuration;
using IntoChat.OS.Framework.Modules.Configuration;

namespace IntoChat.OS.Modules.Microsoft.Azure.Install;

// The ordered specs of the OS foundation (the OS silo, its storage, vault and identity):
// resource group, identity, logs, storage, vault, environment, grants, secret, then the OS app.
public static class InstallPlan
{
    public const string MasterKeySecret = "master-key";
    public const string PostgresPasswordSecret = "postgres-admin-password";
    public const string DbConnectionSecret = "db-connection";
    public const string GhcrTokenSecret = "ghcr-read-token";
    public const string GhcrServer = "ghcr.io";
    public const string BrainLogShare = "brainlog";
    public const string Owner = "os";
    public const int HttpPort = 8080;
    // The Orleans gateway. The silo port is deliberately not mapped: a Container Apps port mapping is
    // load-balanced, while silos must reach each other's own addresses.
    public const int GatewayPort = 30000;

    private const string KeyVaultSecretsOfficer = "b86a8fe4-44ce-4948-aee5-eccb2c155cd7";
    private const string KeyVaultSecretsUser = "4633458b-17de-408a-b874-0445c86b69e6";
    private const string AcrPull = "7f951dda-4ed3-4680-a7ca-43fe172d538d";
    private const string StorageBlobDataContributor = "ba92f5b4-2d11-453d-a403-e96b0029c9fe";
    private const string StorageTableDataContributor = "0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3";
    private const string BrainLogVolume = "brainlog";
    private const int AppUid = 1654;
    private const int AppGid = 1654;

    public static ResourceSpec[] Build(InstallConfig config) => Build(config, new Dictionary<string, string>(), []);

    // Everything the OS app runs on: its resources, without the app itself.
    public static ResourceSpec[] Foundation(InstallConfig config) => Build(config)[..^1];

    // The OS app, with the variables and secrets the module deployments asked for.
    public static ContainerAppSpec OsApp(InstallConfig config, IReadOnlyDictionary<string, string> variables, IReadOnlyList<SecretRef> secrets) =>
        (ContainerAppSpec)Build(config, variables, secrets)[^1];

    public static FoundationRefs Refs(InstallConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var c = config.Validated();
        var group = c.GroupName;
        ResourceRef Ref(string kind, string name) => new(ResourceIds.For(c.Subscription, kind, group, name), kind, name, group);
        return new FoundationRefs(
            Ref(ResourceKinds.ResourceGroup, group),
            Ref(ResourceKinds.Identity, c.IdentityName),
            Ref(ResourceKinds.ManagedEnvironment, c.EnvironmentName),
            Ref(ResourceKinds.StorageAccount, c.StorageName),
            Ref(ResourceKinds.KeyVault, c.VaultName),
            c.LoginServer,
            c.Registry.Enabled ? Ref(ResourceKinds.Registry, c.RegistryName) with { Endpoint = c.LoginServer } : null,
            c.Postgres.Enabled ? Ref(ResourceKinds.Postgres, c.PostgresName) with { Endpoint = PostgresHost(c) } : null);
    }

    public static string PostgresHost(InstallConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.PostgresName + ".postgres.database.azure.com";
    }

    private static ResourceSpec[] Build(InstallConfig config, IReadOnlyDictionary<string, string> extraVariables, IReadOnlyList<SecretRef> extraSecrets)
    {
        ArgumentNullException.ThrowIfNull(config);
        var c = config.Validated();
        var group = c.GroupName;
        var identity = c.IdentityName;
        var workspace = c.LogsName;
        var account = c.StorageName;
        var vault = c.VaultName;
        var environment = c.EnvironmentName;
        var app = c.OsName;
        var registry = c.RegistryName;
        var grainState = "grainstate-" + c.StorageFormat;
        var lease = "brainlease-" + c.StorageFormat;
        var scopePrefix = $"/subscriptions/{c.Subscription}/resourceGroups/{group}/providers";
        var vaultScope = $"{scopePrefix}/Microsoft.KeyVault/vaults/{vault}";
        var accountScope = $"{scopePrefix}/Microsoft.Storage/storageAccounts/{account}";
        var registryScope = $"{scopePrefix}/Microsoft.ContainerRegistry/registries/{registry}";

        T In<T>(T spec) where T : ResourceSpec => spec with { ResourceGroup = group, Location = c.Location, Env = c.Env, Owner = Owner };
        RoleGrantSpec Grant(string name, string role, string scope) =>
            In(new RoleGrantSpec { Name = $"{app}-{name}", Principal = identity, RoleDefinitionId = role, Scope = scope });

        var specs = new List<ResourceSpec>
        {
            In(new ResourceGroupSpec { Name = group }),
            In(new IdentitySpec { Name = identity }),
            In(new LogWorkspaceSpec { Name = workspace }),
            In(new StorageAccountSpec { Name = account }),
            In(new FileShareSpec { Name = BrainLogShare, StorageAccount = account, QuotaGb = c.StorageQuotaGb }),
            In(new BlobContainerSpec { Name = grainState, StorageAccount = account }),
            In(new BlobContainerSpec { Name = lease, StorageAccount = account }),
        };
        if (c.Registry.Enabled) { specs.Add(In(new RegistrySpec { Name = registry, Sku = c.Registry.Sku })); }
        specs.Add(In(new KeyVaultSpec { Name = vault }));
        if (c.InstallerPrincipalId.Length > 0)
        {
            specs.Add(In(new RoleGrantSpec { Name = $"{app}-installer-secrets", Principal = c.InstallerPrincipalId, RoleDefinitionId = KeyVaultSecretsOfficer, Scope = vaultScope }));
        }
        if (c.Postgres.Enabled) { specs.AddRange(PostgresSpecs(c, vault).Select(In)); }
        specs.Add(In(new ManagedEnvironmentSpec
        {
            Name = environment,
            LogWorkspace = workspace,
            Storages = [new EnvironmentStorageSpec { Name = BrainLogShare, StorageAccount = account, ShareName = BrainLogShare }],
        }));
        specs.Add(Grant("vault-secrets", KeyVaultSecretsUser, vaultScope));
        specs.Add(Grant("storage-blobs", StorageBlobDataContributor, accountScope));
        specs.Add(Grant("storage-tables", StorageTableDataContributor, accountScope));
        if (c.Registry.Enabled) { specs.Add(Grant("registry-pull", AcrPull, registryScope)); }
        // Losing the master key loses the brain: a later install never overwrites it.
        specs.Add(In(new SecretSpec { Name = MasterKeySecret, Vault = vault, Overwrite = false }));
        // The installer fills the value when the secrets file has one; the token is then replaced.
        if (c.GhcrUsername.Length > 0) { specs.Add(In(new SecretSpec { Name = GhcrTokenSecret, Vault = vault, Overwrite = true })); }
        specs.Add(In(new ContainerAppSpec
        {
            Name = app,
            Environment = environment,
            Identity = identity,
            Image = c.OsImage,
            Cpu = c.Cpu,
            Memory = c.Memory,
            IngressPort = HttpPort,
            InternalPorts = [GatewayPort],
            RegistryServer = c.LoginServer,
            // Liveness is the process alone, readiness the drain, startup waits for dependencies.
            ProbePaths = [HostProbePaths.Alive, HostProbePaths.Ready, HostProbePaths.Health],
            Variables = Variables(c, account, vault, lease, grainState, extraVariables),
            Secrets = [new SecretRef { Name = MasterKeySecret, Vault = vault, VaultSecret = MasterKeySecret, Variable = IntoChatOSNames.MasterKeyEnvironmentVariable }, .. extraSecrets],
            VolumeMounts =
            [
                new VolumeMountSpec
                {
                    Volume = BrainLogVolume,
                    EnvironmentStorage = BrainLogShare,
                    MountPath = BrainLogDirectory(c),
                    // nobrl is deliberately absent: lease ownership does not need it. actimeo is absent because Container Apps
                    // refuses it (ContainerAppVolumeMountOptionsNotSupported); cache=strict keeps attribute reads coherent.
                    MountOptions = string.Create(CultureInfo.InvariantCulture, $"dir_mode=0700,file_mode=0600,uid={AppUid},gid={AppGid},mfsymlinks,cache=strict,nosharesock"),
                },
            ],
            Registries = c.GhcrUsername.Length > 0
                ? [new RegistryCredentialSpec { Server = GhcrServer, Username = c.GhcrUsername, Vault = vault, PasswordSecret = GhcrTokenSecret }]
                : [],
        }));
        return [.. specs];
    }

    // The admin password is generated once (Overwrite false) unless the secrets file carries it. db-connection is
    // rewritten on every install from the server host and that password, so apps read one secret.
    private static IEnumerable<ResourceSpec> PostgresSpecs(InstallConfig c, string vault)
    {
        yield return new SecretSpec { Name = PostgresPasswordSecret, Vault = vault, Overwrite = false };
        yield return new PostgresSpec
        {
            Name = c.PostgresName,
            Sku = c.Postgres.Sku,
            Tier = c.Postgres.Tier,
            StorageGb = c.Postgres.StorageGb,
            AdminLogin = c.Postgres.AdminLogin,
            Vault = vault,
            AdminPasswordSecret = PostgresPasswordSecret,
        };
        foreach (var database in c.Postgres.Databases)
        {
            yield return new PostgresDatabaseSpec { Name = database, Server = c.PostgresName };
        }
        for (var i = 0; i < c.Postgres.Databases.Length; i++)
        {
            var database = c.Postgres.Databases[i];
            yield return new SecretSpec
            {
                Name = i == 0 ? DbConnectionSecret : $"{DbConnectionSecret}-{database}",
                Vault = vault,
                Overwrite = true,
                Value = $"Host={PostgresHost(c)};Database={database};Username={c.Postgres.AdminLogin};Password={SecretSpec.Reference(PostgresPasswordSecret)}",
            };
        }
    }

    public static string DryRunLine(ResourceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return $"ensure {spec.Kind} {spec.Name} in {spec.ResourceGroup}";
    }

    public static string Summary(IReadOnlyCollection<ResourceSpec> specs)
    {
        ArgumentNullException.ThrowIfNull(specs);
        var counts = specs.GroupBy(spec => spec.Kind).OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => $"{group.Key}={group.Count()}");
        return $"summary: {specs.Count} resources in {string.Join(", ", specs.Select(spec => spec.ResourceGroup).Distinct())} ({string.Join(", ", counts)})";
    }

    private static Dictionary<string, string> Variables(InstallConfig c, string account, string vault, string lease, string grainState, IReadOnlyDictionary<string, string> extra)
    {
        var blobs = $"https://{account}.blob.core.windows.net/";
        var variables = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            // A hosted deployment declares its topology: the hosted posture rules apply, not the local loopback rule (D-21).
            [Variable(IntoChatOSNames.TopologyKey)] = IntoChatOSNames.TopologyCloud,
            // DefaultAzureCredential picks the user-assigned identity by this client id (Orleans stores, lease, vault).
            ["AZURE_CLIENT_ID"] = ContainerAppSpec.IdentityClientIdToken,
            // The cloud topology's account (its blob URI: tables and queues derive from it, token auth with the identity) and vault (P2-D-22).
            [Variable(IntoChatOSNames.CloudStorageConfigurationKey)] = blobs,
            [Variable(IntoChatOSNames.CloudKeyVaultConfigurationKey)] = $"https://{vault}.vault.azure.net/",
            ["Orleans__ClusterId"] = c.ClusterId,
            ["Orleans__ServiceId"] = c.ServiceId,
            // The mounted share is the Home; the brain log lives in <Home>/state.
            [Variable(IntoChatOSNames.HomeKey)] = BrainLogDirectory(c),
            [Variable(IntoChatOSNames.ConfiguredCustodyAcknowledgedConfigurationKey)] = "true",
            // The grain state stays in this install's per-format container (the orleans module option).
            [OrleansOption(OrleansIntegrationNames.GrainStateContainer)] = grainState,
            [Variable(IntoChatOSNames.BrainStorageOwnershipKindConfigurationKey)] = IntoChatOSNames.BrainStorageOwnershipBlobLease,
            [Variable(IntoChatOSNames.BrainStorageOwnershipContainerUriConfigurationKey)] = blobs + lease,
        };
        foreach (var (name, value) in extra) { variables[name] = value; }
        return variables;
    }

    private static string BrainLogDirectory(InstallConfig c) => "/data/brain/" + c.StorageFormat;

    private static string Variable(string configurationKey) => ConfigurationKeyGrammar.EnvironmentName(configurationKey);

    private static string OrleansOption(string option)
        => Variable(IntoChat.OS.Framework.Modules.Configuration.IntoChatOSConfiguration.ModuleOptionsSection(OrleansIntegrationNames.Id) + ":" + option);
}
