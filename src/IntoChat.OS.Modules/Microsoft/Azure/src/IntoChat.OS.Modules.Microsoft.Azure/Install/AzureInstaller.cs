using System.Security.Cryptography;
using System.Text.Json;
using IntoChat.OS.Framework.Modules;
using Azure;

namespace IntoChat.OS.Modules.Microsoft.Azure.Install;

// dotnet run --project IntoChat.OS.Kernel -- install --env test --config deployment/test.json [--dry-run] [--print-env] [--secrets-from file]
// Runs before any cluster exists: the specs are ensured straight against ARM, in dependency order, and the
// deployments of the composed modules run after the foundation.
public static class AzureInstaller
{
    public const string MasterKeyVariable = "INTOCHATOS_MASTER_KEY";

    public static bool IsInstall(string[] args) => args is [var verb, ..] && verb == "install";

    public static async Task<int> Run(string[] args, IAzureArm arm, IEnumerable<OsModule> modules, TextWriter output, TextWriter error, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(arm);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        InstallConfig config;
        bool dryRun, printEnv;
        string? secretsFrom;
        try
        {
            var options = Options.Parse(args);
            (dryRun, secretsFrom, printEnv) = (options.DryRun, options.SecretsFrom, options.PrintEnv);
            config = InstallConfig.Parse(await File.ReadAllTextAsync(options.Config, ct));
            if (!string.Equals(config.Env, options.Env, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"--env '{options.Env}' does not match the config's env '{config.Env}'.");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or JsonException)
        {
            await error.WriteLineAsync($"install: {ex.Message}", ct);
            return 2;
        }

        var manifests = ManifestEnvironment.ManifestsOf(modules);
        if (printEnv)
        {
            // R4-03: the names the OS app will carry, rendered from the profile's foundation and the composed manifests. No Azure call.
            try
            {
                foreach (var name in ManifestEnvironment.EnvironmentNames(config, manifests)) { await output.WriteLineAsync(name, ct); }
                return 0;
            }
            catch (InvalidOperationException ex)
            {
                await error.WriteLineAsync($"install: {ex.Message}", ct);
                return 2;
            }
        }

        IReadOnlyList<ModuleDeploymentBinding> deployments;
        try { deployments = ModuleDeployments.Discover(modules); }
        catch (InvalidOperationException ex)
        {
            await error.WriteLineAsync($"install: {ex.Message}", ct);
            return 2;
        }

        var provided = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!dryRun)
        {
            try { provided = await SecretValues(secretsFrom, ct); }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or JsonException)
            {
                await error.WriteLineAsync($"install: {ex.Message}", ct);
                return 2;
            }
        }

        var current = "";
        var planned = new List<ResourceSpec>();
        async Task<ResourceRef> Ensure(ResourceSpec spec, CancellationToken token)
        {
            current = InstallPlan.DryRunLine(spec);
            await output.WriteLineAsync(current, token);
            planned.Add(spec);
            if (dryRun) { return new ResourceRef(ResourceIds.For(config.Subscription, spec), spec.Kind, spec.Name, spec.ResourceGroup); }
            return await arm.Ensure(config.Subscription, AzureTags.Tagged(spec), token);
        }

        try
        {
            // The foundation first, then each composed module's deployment, then the OS app with what they asked for.
            var foundation = InstallPlan.Refs(config);
            foreach (var spec in InstallPlan.Foundation(config))
            {
                var made = await Ensure(dryRun ? spec : WithValue(spec, provided), ct);
                if (spec is RegistrySpec && made.Endpoint.Length > 0)
                {
                    foundation = foundation with { RegistryServer = made.Endpoint, Registry = made };
                }
            }
            var target = new DeploymentTarget(config.Subscription, config.Location, config.Prefix, config.Env);
            var variables = new Dictionary<string, string>(StringComparer.Ordinal);
            var secrets = new List<SecretRef>();
            foreach (var (module, deployment) in deployments)
            {
                var context = new ModuleDeploymentContext(target, foundation, module, config.SettingsFor(module), Ensure, ct, provided, dryRun,
                    (vault, name, token) => arm.ReadSecret(vault, name, token));
                current = $"deploy module {module}";
                await deployment.Deploy(context);
                foreach (var (name, value) in context.RuntimeVariables) { variables[name] = value; }
                secrets.AddRange(context.RuntimeSecrets);
            }
            // The manifests' own settings (values the deployment input supplies) and secrets (Key Vault references).
            var (settingVariables, manifestSecrets) = ManifestEnvironment.Render(config, manifests);
            foreach (var (name, value) in settingVariables) { variables[name] = value; }
            foreach (var secret in manifestSecrets)
            {
                // A reference to a vault secret that does not exist stops the container app from starting: fail here, before it.
                if (!dryRun && await arm.ReadSecret(secret.Vault, secret.VaultSecret, ct) is null)
                {
                    throw new InvalidOperationException($"the Key Vault secret '{secret.VaultSecret}' of a composed module is not in {secret.Vault}; store it before installing.");
                }
                secrets.Add(secret);
            }
            var osApp = InstallPlan.OsApp(config, variables, secrets);
            await Ensure(osApp with { RegistryServer = foundation.RegistryServer }, ct);
            if (dryRun) { await output.WriteLineAsync(InstallPlan.Summary(planned), ct); }
        }
        catch (Exception ex) when (ex is RequestFailedException or InvalidOperationException or NotSupportedException or ArgumentException or FormatException)
        {
            await error.WriteLineAsync($"install: {current} failed: {ex.Message}", ct);
            return 1;
        }
        return 0;
    }

    // The master key comes from the file or the environment when given; otherwise the vault generates it once and keeps it. The Postgres admin password is generated once
    // when neither has it (the spec is Overwrite=false, so a later install keeps the stored one); the GHCR token is optional.
    private static async Task<Dictionary<string, string>> SecretValues(string? secretsFrom, CancellationToken ct)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (secretsFrom is not null)
        {
            foreach (var (name, value) in JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(secretsFrom, ct)) ?? [])
            {
                if (!string.IsNullOrWhiteSpace(value)) { values[name] = value; }
            }
        }
        if (!values.ContainsKey(InstallPlan.MasterKeySecret))
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(MasterKeyVariable);
            if (!string.IsNullOrEmpty(fromEnvironment)) { values[InstallPlan.MasterKeySecret] = fromEnvironment; }
        }
        if (!values.ContainsKey(InstallPlan.PostgresPasswordSecret))
        {
            values[InstallPlan.PostgresPasswordSecret] = GeneratedPassword();
        }
        return values;
    }

    private static ResourceSpec WithValue(ResourceSpec spec, Dictionary<string, string> provided) => spec switch
    {
        SecretSpec { Name: InstallPlan.GhcrTokenSecret } secret => provided.TryGetValue(secret.Name, out var token)
            ? secret with { Value = token }
            : secret with { Overwrite = false },
        SecretSpec { Name: InstallPlan.MasterKeySecret, Value.Length: 0 } secret when !provided.ContainsKey(secret.Name) =>
            secret with { Generator = SecretGenerators.Base64(32) },
        SecretSpec { Value.Length: 0 } secret when provided.TryGetValue(secret.Name, out var value) => secret with { Value = value },
        _ => spec,
    };

    // 32 letters and digits: valid for Postgres and safe inside a connection string.
    private static string GeneratedPassword()
    {
        const string alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return string.Create(32, alphabet, static (span, letters) =>
        {
            for (var i = 0; i < span.Length; i++) { span[i] = letters[RandomNumberGenerator.GetInt32(letters.Length)]; }
        });
    }

    private sealed record Options(string Env, string Config, bool DryRun, string? SecretsFrom, bool PrintEnv)
    {
        public static Options Parse(string[] args)
        {
            string? env = null, config = null, secrets = null;
            var dryRun = false;
            var printEnv = false;
            for (var i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--env": env = Value(args, ref i); break;
                    case "--config": config = Value(args, ref i); break;
                    case "--secrets-from": secrets = Value(args, ref i); break;
                    case "--dry-run": dryRun = true; break;
                    case "--print-env": printEnv = true; break;
                    default: throw new ArgumentException($"Unknown install argument '{args[i]}'.");
                }
            }
            return new Options(
                env ?? throw new ArgumentException("install needs --env."),
                config ?? throw new ArgumentException("install needs --config."),
                dryRun,
                secrets,
                printEnv);
        }

        private static string Value(string[] args, ref int i) =>
            ++i < args.Length ? args[i] : throw new ArgumentException($"{args[i - 1]} needs a value.");
    }
}
