using System.Text.Json;

namespace IntoChat.OS.Modules.Microsoft.Azure.Install;

// deployment/<env>.json: what the OS foundation needs that the code cannot know.
public sealed record InstallConfig
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { ReadCommentHandling = JsonCommentHandling.Skip };

    public string Subscription { get; init; } = "";
    public string Location { get; init; } = "";
    public string Prefix { get; init; } = "";
    public string Env { get; init; } = "";
    public string OsImage { get; init; } = "";
    public double Cpu { get; init; } = 0.5;
    public string Memory { get; init; } = "1Gi";
    public int StorageQuotaGb { get; init; } = 20;
    // Optional: the registry that holds OsImage, pulled with the runtime identity.
    public string RegistryServer { get; init; } = "";
    // Orleans__ClusterId and Orleans__ServiceId; default to intochat-<env>.
    public string ClusterId { get; init; } = "";
    public string ServiceId { get; init; } = "";
    // Suffix of every versioned store: grainstate-v1, brainlease-v1, /data/brain/v1.
    public string StorageFormat { get; init; } = "v1";
    // Optional explicit names. Empty means the derived <prefix>-<env>-<kind>.
    public string ResourceGroup { get; init; } = "";
    // Keys: os, identity, storage, vault, environment, logs, registry, postgres.
    public Dictionary<string, string> Names { get; init; } = [];
    public RegistryConfig Registry { get; init; } = new();
    public PostgresConfig Postgres { get; init; } = new();
    // Object id of the principal running the install (the gh-intochat-platform OIDC app). It is granted Key Vault
    // Secrets Officer on the vault so the installer can read and write secret values on the data plane.
    public string InstallerPrincipalId { get; init; } = "";
    // When set, the OS app also pulls from ghcr.io as this user with the Key Vault secret ghcr-read-token.
    public string GhcrUsername { get; init; } = "";
    // Settings per module id, read by that module's deployment: { "csharp": { "sandboxImage": "..." } }.
    public Dictionary<string, Dictionary<string, string>> Modules { get; init; } = [];

    public static InstallConfig Parse(string json)
    {
        var config = JsonSerializer.Deserialize<InstallConfig>(json, Json) ?? throw new InvalidOperationException("The install config is empty.");
        return config.Validated();
    }

    public string GroupName => ResourceGroup.Length > 0 ? ResourceGroup : ResourceNames.ResourceGroup(Prefix, Env);

    public string IdentityName => Named("identity", ResourceNames.Identity(Prefix, Env));

    public string LogsName => Named("logs", ResourceNames.LogWorkspace(Prefix, Env));

    public string StorageName => Named("storage", ResourceNames.StorageAccount(Prefix, Env));

    public string VaultName => Named("vault", ResourceNames.KeyVault(Prefix, Env));

    public string EnvironmentName => Named("environment", ResourceNames.Environment(Prefix, Env));

    public string OsName => Named("os", ResourceNames.App(Prefix, Env, "os"));

    // names.registry, else the first label of registryServer (an install config that already names its registry), else derived.
    public string RegistryName => Named("registry", RegistryServer.Length > 0 ? RegistryServer.Split('.')[0] : ResourceNames.Registry(Prefix, Env));

    public string PostgresName => Named("postgres", ResourceNames.Postgres(Prefix, Env));

    // The login server of the registry: the config's registryServer when given, else <registry name>.azurecr.io when one is created.
    public string LoginServer => RegistryServer.Length > 0 ? RegistryServer : Registry.Enabled ? RegistryName + ".azurecr.io" : "";

    private string Named(string key, string derived)
    {
        var entry = Names.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(entry.Value) ? derived : entry.Value;
    }

    public ModuleSettings SettingsFor(string module)
    {
        var entry = Modules.FirstOrDefault(pair => string.Equals(pair.Key, module, StringComparison.OrdinalIgnoreCase));
        return entry.Value is null ? ModuleSettings.Empty : new ModuleSettings(entry.Value);
    }

    public InstallConfig Validated()
    {
        foreach (var (name, value) in new[]
        {
            ("subscription", Subscription), ("location", Location), ("prefix", Prefix), ("env", Env), ("osImage", OsImage),
        })
        {
            if (string.IsNullOrWhiteSpace(value)) { throw new InvalidOperationException($"The install config needs '{name}'."); }
        }
        if (Cpu <= 0 || StorageQuotaGb <= 0) { throw new InvalidOperationException("The install config needs a positive cpu and storageQuotaGb."); }
        if (Postgres.Enabled && (Postgres.StorageGb <= 0 || Postgres.AdminLogin.Length == 0))
        {
            throw new InvalidOperationException("The postgres config needs a positive storageGb and an adminLogin.");
        }
        return this with
        {
            ClusterId = ClusterId.Length > 0 ? ClusterId : "intochat-" + Env,
            ServiceId = ServiceId.Length > 0 ? ServiceId : "intochat-" + Env,
            Postgres = Postgres with { AdminLogin = Postgres.AdminLogin.Length > 0 ? Postgres.AdminLogin : "dbadmin" },
        };
    }
}

// registry: { "enabled": true, "sku": "Basic" }. Enabled by default.
public sealed record RegistryConfig
{
    public bool Enabled { get; init; } = true;
    public string Sku { get; init; } = "Basic";
}

// postgres: { "enabled": true, "sku": "Standard_B1ms", "tier": "Burstable", "storageGb": 32, "adminLogin": "dbadmin", "databases": ["intochat"] }.
// Disabled by default. The first database is written to the Key Vault secret db-connection.
public sealed record PostgresConfig
{
    public bool Enabled { get; init; }
    public string Sku { get; init; } = "Standard_B1ms";
    public string Tier { get; init; } = "Burstable";
    public int StorageGb { get; init; } = 32;
    public string AdminLogin { get; init; } = "dbadmin";
    public string[] Databases { get; init; } = [];
}
