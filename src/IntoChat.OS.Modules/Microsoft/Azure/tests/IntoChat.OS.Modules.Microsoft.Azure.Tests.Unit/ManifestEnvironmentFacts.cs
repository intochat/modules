using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Framework.Modules.Configuration;
using IntoChat.OS.Framework.Modules.Platform.Integrations;
using IntoChat.OS.Modules.Microsoft.Azure.Install;
using IntoChat.OS.ServiceDefaults;

namespace IntoChat.OS.Modules.Microsoft.Azure.Tests.Unit;

// R4-03 (09-10): the OS app's environment is rendered from module manifests and the deployment input, never hand-listed.
public sealed class ManifestEnvironmentFacts
{
    public sealed class AuthOptions
    {
        public string? PublicBaseUrl { get; set; }
        public string? License { get; set; }
        public int PasswordIterations { get; set; }
        public string ReadOnly { get; } = "";
    }

    [DigitalBrain.Module("auth")]
    public sealed class AuthModule : DigitalBrain.Module
    {
        public static readonly string Section = IntoChatOSConfiguration.ModuleOptionsSection("auth");

        public static DigitalBrain.ModuleManifest Manifest { get; } = new(
            Provides: [], Requires: [], Needs: [],
            Settings: [DigitalBrain.Setting.Of<AuthOptions>(IntoChatOSConfiguration.ModuleOptionsSection("auth"))],
            Secrets: [DigitalBrain.Secret.Named("signin-issuer"), DigitalBrain.Secret.Named("acs-email")]);
    }

    private static string Name(string property) => ConfigurationKeyGrammar.EnvironmentName(AuthModule.Section + ":" + property);

    private static DigitalBrain.ModuleManifest[] Manifests => [DigitalBrain.ModuleManifest.Of(typeof(AuthModule))];

    private static InstallConfig Config(string modules = "") => InstallConfig.Parse("""
        {"subscription":"00000000-0000-0000-0000-000000000001","location":"westeurope","prefix":"intochat","env":"test","osImage":"ghcr.io/intochat/os:1.0.0"
        """ + (modules.Length > 0 ? ",\"modules\":" + modules : "") + "}");

    [Fact]
    public void A_setting_with_a_value_becomes_its_configuration_key_variable_and_one_without_does_not()
    {
        var (variables, _) = ManifestEnvironment.Render(Config("""{"auth":{"PublicBaseUrl":"https://app.test","PasswordIterations":"1000"}}"""), Manifests);
        Assert.Equal("https://app.test", variables[Name("PublicBaseUrl")]);
        Assert.Equal("1000", variables[Name("PasswordIterations")]);
        Assert.DoesNotContain(Name("License"), variables.Keys);
        Assert.DoesNotContain(variables.Keys, name => name.EndsWith("ReadOnly", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_declared_secret_is_a_key_vault_reference_named_module_dash_secret()
    {
        var config = Config();
        var (_, secrets) = ManifestEnvironment.Render(config, Manifests);
        Assert.Equal(["auth-signin-issuer", "auth-acs-email"], secrets.Select(secret => secret.Name));
        Assert.All(secrets, secret =>
        {
            Assert.Equal(secret.Name, secret.VaultSecret);
            Assert.Equal(config.VaultName, secret.Vault);
        });
        Assert.Equal(ManifestEnvironment.SecretVariable("auth", "signin-issuer"), secrets[0].Variable);
    }

    [Fact]
    public void An_entry_no_manifest_setting_names_stays_with_the_module_deployment()
    {
        var (variables, _) = ManifestEnvironment.Render(Config("""{"auth":{"Nope":"x"},"gone":{"A":"x"}}"""), Manifests);
        Assert.DoesNotContain(variables.Keys, name => name.Contains("Nope", StringComparison.Ordinal) || name.Contains("gone", StringComparison.Ordinal));
    }

    [Fact]
    public void The_environment_names_carry_the_three_cloud_keys_together_with_the_module_names()
    {
        var names = ManifestEnvironment.EnvironmentNames(Config("""{"auth":{"License":"token"}}"""), Manifests);
        foreach (var key in new[] { IntoChatOSNames.TopologyKey, IntoChatOSNames.CloudStorageConfigurationKey, IntoChatOSNames.CloudKeyVaultConfigurationKey })
        {
            Assert.Contains(ConfigurationKeyGrammar.EnvironmentName(key), names);
        }
        Assert.Contains(Name("License"), names);
        Assert.Contains(ManifestEnvironment.SecretVariable("auth", "acs-email"), names);
        Assert.Equal(names.Order(StringComparer.Ordinal), names);
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    [Fact]
    public async Task Print_env_lists_the_names_without_contacting_azure()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var file = Path.Combine(directory.FullName, "test.json");
            await File.WriteAllTextAsync(file, """{"subscription":"00000000-0000-0000-0000-000000000001","location":"westeurope","prefix":"intochat","env":"test","osImage":"ghcr.io/intochat/os:1.0.0"}""", TestContext.Current.CancellationToken);
            var arm = new RecordingAzure();
            var output = new StringWriter();

            var code = await AzureInstaller.Run(["install", "--env", "test", "--config", file, "--print-env"], arm, [], output, new StringWriter(), TestContext.Current.CancellationToken);

            Assert.Equal(0, code);
            Assert.Empty(arm.Ensured);
            Assert.Empty(arm.VaultReads);
            var names = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(ManifestEnvironment.EnvironmentNames(InstallConfig.Parse(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken)), []), names);
            Assert.Contains(ConfigurationKeyGrammar.EnvironmentName(IntoChatOSNames.CloudKeyVaultConfigurationKey), names);
        }
        finally { directory.Delete(true); }
    }

    [DigitalBrain.Module("github")]
    public sealed class GitHubIntegrationModule : DigitalBrain.Module
    {
        public static DigitalBrain.ModuleManifest Manifest { get; } = new(Provides: [], Requires: [], Needs: [], Settings: [], Secrets: []);

        public static IntegrationSchema Integration { get; } = IntegrationSchema
            .For("github.app", "GitHub")
            .RequiresSecret("ClientSecret")
            .OffersSecret("WebhookSecret");
    }

    [Fact]
    public void Integration_schemas_project_normalized_vault_secret_names_and_integration_env_variables()
    {
        var modules = new[] { OsModule.FromDigital(typeof(GitHubIntegrationModule)) };
        var secrets = ManifestEnvironment.IntegrationSecretsOf(modules, "kv-intochat-test");

        Assert.Equal(2, secrets.Count);
        Assert.Equal("github-app-clientsecret", secrets[0].VaultSecret);
        Assert.Equal("IntoChatOS__Integrations__github.app__ClientSecret", secrets[0].Variable);
        Assert.Equal("kv-intochat-test", secrets[0].Vault);
        Assert.Equal("github-app-webhooksecret", secrets[1].VaultSecret);
        Assert.Equal("IntoChatOS__Integrations__github.app__WebhookSecret", secrets[1].Variable);
    }
}
