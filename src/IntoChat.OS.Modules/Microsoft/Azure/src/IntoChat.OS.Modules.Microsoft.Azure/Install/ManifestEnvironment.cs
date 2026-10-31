using System.Reflection;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework.Modules.Configuration;
using IntoChat.OS.Framework.Modules.Platform.Integrations;

namespace IntoChat.OS.Modules.Microsoft.Azure.Install;

// R4-03 (09-10): the container environment of the OS app for the modules a deployment composes comes from their static
// ModuleManifest, not from a hand-written list. A Setting yields the variable of its configuration key
// ({Section}:{Property}, section default IntoChatOS:Modules:{id}:Options) when the deployment input supplies a value for
// that property (InstallConfig.Modules[id][Property]); a Secret yields a Key Vault reference named {id}-{secret} carried
// by the module's Secrets section (IntoChatOS__Modules__{id}__Secrets__{secret}). Nothing here reads or writes a secret value: the vault holds those.
public static class ManifestEnvironment
{
    // The static ModuleManifest of every composed module that declares one (a public static Manifest property), with the module's id.
    public static IReadOnlyList<DigitalBrain.ModuleManifest> ManifestsOf(IEnumerable<IntoChat.OS.Framework.Modules.OsModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        return [.. modules.GroupBy(module => module.Type).Select(group => group.First())
            .Select(module => module.Type.GetProperty("Manifest", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is DigitalBrain.ModuleManifest manifest
                ? manifest with { Id = module.Id.Value } : null)
            .OfType<DigitalBrain.ModuleManifest>()];
    }

    // The vault secret of a module's declared secret.
    public static string SecretName(string moduleId, string secret) => moduleId + "-" + secret;

    // The variable the container app carries the secret under.
    public static string SecretVariable(string moduleId, string secret)
        => ConfigurationKeyGrammar.EnvironmentName(IntoChatOSConfiguration.ModuleSection(moduleId) + ":Secrets:" + secret);

    // The vault secret of an integration schema's secret field (normalized to lowercase ASCII letters, digits, and dashes).
    public static string IntegrationSecretName(string integrationId, string field)
        => new([.. $"{integrationId}-{field}".ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]);

    // The variable the container app carries an integration secret under (IntoChatOS__Integrations__{id}__{field}).
    public static string IntegrationSecretVariable(string integrationId, string field)
        => ConfigurationKeyGrammar.EnvironmentName(IntegrationSchema.Key(integrationId, field));

    // Every integration secret declared across composed modules' IntegrationSchemas.
    public static IReadOnlyList<SecretRef> IntegrationSecretsOf(IEnumerable<IntoChat.OS.Framework.Modules.OsModule> modules, string vaultName)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultName);
        return [.. modules
            .GroupBy(module => module.Type)
            .Select(group => group.First().Type)
            .SelectMany(SchemasOf)
            .GroupBy(schema => schema.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .SelectMany(schema => schema.SecretFields.Concat(schema.OptionalSecretFields).Select(field =>
            {
                var name = IntegrationSecretName(schema.Id, field);
                return new SecretRef
                {
                    Name = name,
                    Vault = vaultName,
                    VaultSecret = name,
                    Variable = IntegrationSecretVariable(schema.Id, field),
                };
            }))];
    }

    private static IEnumerable<IntegrationSchema> SchemasOf(Type moduleType)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        if (moduleType.GetProperty("Integration", Flags)?.GetValue(null) is IntegrationSchema single)
        {
            yield return single;
        }
        if (moduleType.GetProperty("Integrations", Flags)?.GetValue(null) is IEnumerable<IntegrationSchema> many)
        {
            foreach (var schema in many) { yield return schema; }
        }
    }

    public static (IReadOnlyDictionary<string, string> Variables, IReadOnlyList<SecretRef> Secrets) Render(
        InstallConfig config, IReadOnlyList<DigitalBrain.ModuleManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(manifests);
        var c = config.Validated();
        var variables = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var secrets = new List<SecretRef>();
        foreach (var manifest in manifests)
        {
            var id = manifest.Id ?? throw new InvalidOperationException("A composed module's manifest has no id (use ModuleManifest.Of).");
            var values = c.SettingsFor(id);
            var properties = manifest.Settings
                .SelectMany(setting => setting.OptionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(property => property.CanWrite && property.GetIndexParameters().Length == 0)
                    .Select(property => (Setting: setting, Property: property.Name)))
                .ToArray();
            foreach (var (setting, property) in properties)
            {
                if (values.Get(property) is not { } value) { continue; }
                variables[ConfigurationKeyGrammar.EnvironmentName(setting.Section + ":" + property)] = value;
            }
            foreach (var secret in manifest.Secrets)
            {
                var name = SecretName(id, secret.Name);
                secrets.Add(new SecretRef { Name = name, Vault = c.VaultName, VaultSecret = name, Variable = SecretVariable(id, secret.Name) });
            }
        }
        // Entries of InstallConfig.Modules that no manifest setting names are the module deployment's own (SettingsFor), not environment.
        return (variables, secrets);
    }

    // Every environment name the OS app carries for this deployment, sorted: the foundation, the topology keys, the module
    // settings and the secret variables. `install --print-env` prints it and deploy.sh check_environment requires it.
    public static string[] EnvironmentNames(InstallConfig config, IReadOnlyList<DigitalBrain.ModuleManifest> manifests)
    {
        var (variables, secrets) = Render(config, manifests);
        var app = InstallPlan.OsApp(config, variables, secrets);
        return [.. app.Variables.Keys.Concat(app.Secrets.Select(secret => secret.Variable)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }
}
