namespace IntoChat.OS.Modules.Microsoft.Azure;

// A resource the installer or a neuron asks Azure for. Env and Owner become the env and owner tags;
// Tags are merged over managed-by, env and owner by AzureTags.Merge.
public abstract record ResourceSpec
{
    public string Name { get; init; } = "";
    public string ResourceGroup { get; init; } = "";
    public string Location { get; init; } = "";
    public string Env { get; init; } = "";
    public string Owner { get; init; } = "azure";
    public Dictionary<string, string> Tags { get; init; } = [];

    public abstract string Kind { get; }
}

public sealed record ResourceGroupSpec : ResourceSpec
{
    public override string Kind => ResourceKinds.ResourceGroup;
}

// A user-assigned managed identity. Containers reference it by name.
public sealed record IdentitySpec : ResourceSpec
{
    public override string Kind => ResourceKinds.Identity;
}

public sealed record LogWorkspaceSpec : ResourceSpec
{
    public int RetentionDays { get; init; } = 30;

    public override string Kind => ResourceKinds.LogWorkspace;
}

public sealed record StorageAccountSpec : ResourceSpec
{
    public string Sku { get; init; } = "Standard_LRS";

    public override string Kind => ResourceKinds.StorageAccount;
}

public sealed record FileShareSpec : ResourceSpec
{
    public string StorageAccount { get; init; } = "";
    public int QuotaGb { get; init; } = 20;

    public override string Kind => ResourceKinds.FileShare;
}

public sealed record BlobContainerSpec : ResourceSpec
{
    public string StorageAccount { get; init; } = "";

    public override string Kind => ResourceKinds.BlobContainer;
}

// Role-based access: data-plane access comes from RoleGrantSpec, not access policies.
public sealed record KeyVaultSpec : ResourceSpec
{
    public string TenantId { get; init; } = "";

    public override string Kind => ResourceKinds.KeyVault;
}

// Overwrite false keeps an existing value: a master key is never replaced by a later install.
public sealed record SecretSpec : ResourceSpec
{
    public string Vault { get; init; } = "";
    public string Value { get; init; } = "";
    public bool Overwrite { get; init; }
    // A generator name (SecretGenerators) makes the installer create the value when the secret is absent and never replace it; Value is ignored.
    public string Generator { get; init; } = "";
    // An external secret is set by an operator (az keyvault secret set); the installer only checks that it exists and never writes it.
    public bool External { get; init; }

    // A value of the form "{vault-secret:name}" inside Value is replaced, when the secret is written, by the
    // current value of that secret in the same vault, so a connection string can carry a password only the vault knows.
    public static string Reference(string secret) => "{vault-secret:" + secret + "}";

    public static string[] ReferencesIn(string value) =>
        [.. System.Text.RegularExpressions.Regex.Matches(value, @"\{vault-secret:([^}]+)\}").Select(match => match.Groups[1].Value).Distinct()];

    public static string Substitute(string value, IReadOnlyDictionary<string, string> secrets)
    {
        foreach (var (name, secret) in secrets) { value = value.Replace(Reference(name), secret, StringComparison.Ordinal); }
        return value;
    }

    public override string Kind => ResourceKinds.Secret;
}

public sealed record EnvironmentStorageSpec
{
    public string Name { get; init; } = "";
    public string StorageAccount { get; init; } = "";
    public string ShareName { get; init; } = "";
}

// Workload profiles: Consumption. SubnetId is optional; without it the environment is not VNet-integrated.
public sealed record ManagedEnvironmentSpec : ResourceSpec
{
    public string LogWorkspace { get; init; } = "";
    public string? SubnetId { get; init; }
    public EnvironmentStorageSpec[] Storages { get; init; } = [];

    public override string Kind => ResourceKinds.ManagedEnvironment;
}

public sealed record SecretRef
{
    // Container App secret name, read from Key Vault with the app's identity.
    public string Name { get; init; } = "";
    public string Vault { get; init; } = "";
    public string VaultSecret { get; init; } = "";
    // The environment variable that carries the secret.
    public string Variable { get; init; } = "";
}

public sealed record VolumeMountSpec
{
    public string Volume { get; init; } = "";
    public string EnvironmentStorage { get; init; } = "";
    public string MountPath { get; init; } = "";
    public string MountOptions { get; init; } = "";
}

// Where an app listens. TargetPort 0 means the spec's IngressPort. External false gives environment-internal ingress.
public sealed record IngressSpec
{
    public bool External { get; init; } = true;
    public int TargetPort { get; init; }
}

// A Key Vault secret mounted as one file: an app secret plus a Secret volume and a sub-path mount at Path.
public sealed record SecretFileSpec
{
    // Container App secret name.
    public string Name { get; init; } = "";
    public string Vault { get; init; } = "";
    public string VaultSecret { get; init; } = "";
    // The full path of the file in the container, for example /usr/share/nginx/html/config.json.
    public string Path { get; init; } = "";
}

// A registry the app pulls from. Without PasswordSecret the pull uses an identity (the app's, or Identity by name);
// with it the pull uses Username and the Key Vault secret PasswordSecret in Vault (ghcr-read-token for GHCR).
public sealed record RegistryCredentialSpec
{
    public string Server { get; init; } = "";
    public string Identity { get; init; } = "";
    public string Username { get; init; } = "";
    public string Vault { get; init; } = "";
    public string PasswordSecret { get; init; } = "";
}

public sealed record ContainerAppSpec : ResourceSpec
{
    // The value ArmAzure replaces with the identity's client id (AZURE_CLIENT_ID).
    public const string IdentityClientIdToken = "{identity-client-id}";

    public string Environment { get; init; } = "";
    public string Image { get; init; } = "";
    public double Cpu { get; init; } = 0.5;
    public string Memory { get; init; } = "1Gi";
    public int MinReplicas { get; init; } = 1;
    public int MaxReplicas { get; init; } = 1;
    public Dictionary<string, string> Variables { get; init; } = [];
    public SecretRef[] Secrets { get; init; } = [];
    public string Identity { get; init; } = "";
    public int IngressPort { get; init; } = 8080;
    public int[] InternalPorts { get; init; } = [];
    public VolumeMountSpec[] VolumeMounts { get; init; } = [];
    public string RegistryServer { get; init; } = "";
    public string[] ProbePaths { get; init; } = [];
    public int TerminationGracePeriodSeconds { get; init; } = 60;
    public IngressSpec Ingress { get; init; } = new();
    public SecretFileSpec[] SecretFiles { get; init; } = [];
    public RegistryCredentialSpec[] Registries { get; init; } = [];

    public override string Kind => ResourceKinds.ContainerApp;
}

// Principal is an identity name in the spec's resource group, or a principal id. Scope is a resource id.
public sealed record RoleGrantSpec : ResourceSpec
{
    public string Principal { get; init; } = "";
    public string RoleDefinitionId { get; init; } = "";
    public string Scope { get; init; } = "";

    public override string Kind => ResourceKinds.RoleGrant;
}

public sealed record SecurityRuleSpec
{
    public string Name { get; init; } = "";
    public int Priority { get; init; }
    // Inbound or Outbound.
    public string Direction { get; init; } = "Outbound";
    // Allow or Deny.
    public string Access { get; init; } = "Allow";
    // Tcp, Udp or *.
    public string Protocol { get; init; } = "Tcp";
    public string DestinationAddressPrefix { get; init; } = "*";
    public string DestinationPortRange { get; init; } = "*";
}

public sealed record NetworkSecurityGroupSpec : ResourceSpec
{
    public SecurityRuleSpec[] Rules { get; init; } = [];

    public override string Kind => ResourceKinds.NetworkSecurityGroup;
}

public sealed record VirtualNetworkSpec : ResourceSpec
{
    public string AddressSpace { get; init; } = "10.0.0.0/16";

    public override string Kind => ResourceKinds.VirtualNetwork;
}

// A subnet in a virtual network of the spec's resource group. A delegation service such as
// Microsoft.App/environments reserves the subnet for it.
public sealed record SubnetSpec : ResourceSpec
{
    public string VirtualNetwork { get; init; } = "";
    public string AddressPrefix { get; init; } = "";
    public string NetworkSecurityGroup { get; init; } = "";
    public string DelegationService { get; init; } = "";

    public override string Kind => ResourceKinds.Subnet;
}

// A dynamic custom-container session pool: one Hyper-V session per caller running Image, pulled with
// Identity (which needs AcrPull on the registry). Sessions never see the identity.
public sealed record SessionPoolSpec : ResourceSpec
{
    public string Environment { get; init; } = "";
    public string Image { get; init; } = "";
    public double Cpu { get; init; } = 1;
    public string Memory { get; init; } = "2Gi";
    public int TargetPort { get; init; } = 8080;
    public string Identity { get; init; } = "";
    public string RegistryServer { get; init; } = "";
    public int MaxSessions { get; init; } = 100;
    public int ReadySessions { get; init; } = 2;
    public int MaxAliveSeconds { get; init; } = 86400;
    public bool Egress { get; init; } = true;

    public override string Kind => ResourceKinds.SessionPool;
}

// Basic is enough for a test environment. Admin user stays off: pulls use identities (AcrPull) or a token.
public sealed record RegistrySpec : ResourceSpec
{
    public string Sku { get; init; } = "Basic";

    public override string Kind => ResourceKinds.Registry;
}

// Azure Database for PostgreSQL Flexible Server on a public endpoint with the allow-Azure-services rule.
// The admin password is the Key Vault secret AdminPasswordSecret in Vault.
public sealed record PostgresSpec : ResourceSpec
{
    public string Sku { get; init; } = "Standard_B1ms";
    public string Tier { get; init; } = "Burstable";
    public int StorageGb { get; init; } = 32;
    public string Version { get; init; } = "16";
    public string AdminLogin { get; init; } = "";
    public string Vault { get; init; } = "";
    public string AdminPasswordSecret { get; init; } = "";

    public override string Kind => ResourceKinds.Postgres;
}

public sealed record PostgresDatabaseSpec : ResourceSpec
{
    // The server name, in the spec's resource group.
    public string Server { get; init; } = "";

    public override string Kind => ResourceKinds.PostgresDatabase;
}
