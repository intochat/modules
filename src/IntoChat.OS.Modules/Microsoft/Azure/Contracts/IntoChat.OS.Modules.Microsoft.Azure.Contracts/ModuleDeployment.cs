using System.Globalization;

namespace IntoChat.OS.Modules.Microsoft.Azure;

// A module's cloud needs, claiming its module through the generic parameter. The installer finds every
// deployment of the composed modules and runs it against the foundation; Module is checked against the
// module instances at discovery, so a deployment for a module that is not composed fails loudly.
public interface IModuleDeployment
{
    Type Module { get; }

    Task Deploy(ModuleDeploymentContext context);
}

public abstract class ModuleDeployment<TModule> : IModuleDeployment where TModule : class
{
    public Type Module => typeof(TModule);

    public abstract Task Deploy(ModuleDeploymentContext context);
}

// Where a deployment lands: the subscription and the names every resource derives from.
public sealed record DeploymentTarget(string Subscription, string Location, string Prefix, string Env);

// The shared resources of the environment, created before any module deployment runs. RegistryServer is the
// login server of the registry the installer created (or the config's registryServer when it creates none).
// Registry and Postgres are null when the config disables them.
public sealed record FoundationRefs(
    ResourceRef ResourceGroup,
    ResourceRef Identity,
    ResourceRef Environment,
    ResourceRef StorageAccount,
    ResourceRef KeyVault,
    string RegistryServer,
    ResourceRef? Registry = null,
    ResourceRef? Postgres = null);

// The module's own entries of the install config, keyed by setting name.
public sealed class ModuleSettings(IReadOnlyDictionary<string, string> values)
{
    public static ModuleSettings Empty { get; } = new(new Dictionary<string, string>());

    public string? Get(string name) => values.TryGetValue(name, out var value) && value.Length > 0 ? value : null;

    public string Require(string name) => Get(name) ?? throw new InvalidOperationException($"The module setting '{name}' is required.");

    public int? GetInt32(string name) => Get(name) is { } text
        ? int.Parse(text, CultureInfo.InvariantCulture)
        : null;
}

// What a deployment sees: the foundation, the module's settings, and Ensure, which fills the target's
// resource group, location, env and the module as owner into each spec. A deployment can also add
// variables and secret references to the OS app, which the installer creates after every deployment ran.
public sealed class ModuleDeploymentContext(
    DeploymentTarget target,
    FoundationRefs foundation,
    string owner,
    ModuleSettings settings,
    Func<ResourceSpec, CancellationToken, Task<ResourceRef>> ensure,
    CancellationToken cancellationToken,
    IReadOnlyDictionary<string, string>? secretValues = null,
    bool dryRun = false,
    Func<string, string, CancellationToken, Task<string?>>? readVaultSecret = null)
{
    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);
    private readonly List<SecretRef> _secrets = [];

    public DeploymentTarget Target { get; } = target;
    public FoundationRefs Foundation { get; } = foundation;
    public ModuleSettings Settings { get; } = settings;
    public string Owner { get; } = owner;
    public CancellationToken CancellationToken { get; } = cancellationToken;
    public bool IsDryRun { get; } = dryRun;
    public IReadOnlyDictionary<string, string> RuntimeVariables => _variables;
    public IReadOnlyList<SecretRef> RuntimeSecrets => _secrets;

    // Values only cross into SecretSpec.Value, never ordinary module settings or diagnostic output.
    public string RequireSecret(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (secretValues?.TryGetValue(name, out var value) == true && !string.IsNullOrWhiteSpace(value)) { return value; }
        if (IsDryRun) { return "dry-run"; }
        throw new InvalidOperationException($"The secrets file needs '{name}'.");
    }

    // The current value of a secret in the foundation vault, for values derived at install time (a public key from a private
    // one). Null when absent; a dry run, which never touches Azure, gets null for every secret.
    public Task<string?> ReadVaultSecret(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return IsDryRun || readVaultSecret is null
            ? Task.FromResult<string?>(null)
            : readVaultSecret(Foundation.KeyVault.Name, name, CancellationToken);
    }

    // <prefix>-<env>-<suffix>, the same naming the foundation uses.
    public string Name(string suffix) => ResourceNames.For(Target.Prefix, Target.Env, suffix);

    public Task<ResourceRef> Ensure(ResourceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var filled = spec with
        {
            ResourceGroup = spec.ResourceGroup.Length > 0 ? spec.ResourceGroup : Foundation.ResourceGroup.Name,
            Location = spec.Location.Length > 0 ? spec.Location : Target.Location,
            Env = Target.Env,
            Owner = Owner,
        };
        return ensure(filled, CancellationToken);
    }

    // The runtime identity holds the role on the scope (a resource id).
    public Task<ResourceRef> Grant(string name, string roleDefinitionId, string scope) =>
        Ensure(new RoleGrantSpec { Name = $"{Name(Owner)}-{name}", Principal = Foundation.Identity.Name, RoleDefinitionId = roleDefinitionId, Scope = scope });

    public void SetRuntimeVariable(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _variables[name] = value;
    }

    public void AddRuntimeSecret(SecretRef secret)
    {
        ArgumentNullException.ThrowIfNull(secret);
        _secrets.RemoveAll(existing => existing.Name == secret.Name);
        _secrets.Add(secret);
    }
}
