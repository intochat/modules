using IntoChat.OS.Modules.Microsoft.Azure.Install;

namespace IntoChat.OS.Modules.Microsoft.Azure.Tests.Unit;

public sealed class InstallerGapFacts
{
    private const string Base = """
        "subscription":"00000000-0000-0000-0000-000000000001","location":"polandcentral","prefix":"intochat","env":"test","osImage":"ghcr.io/intochat/os:1.0.0"
        """;

    private static InstallConfig Parse(string extra = "") => InstallConfig.Parse("{" + Base + (extra.Length > 0 ? "," + extra : "") + "}");

    [Fact]
    public void A_config_with_a_resource_group_makes_every_spec_target_it()
    {
        var specs = InstallPlan.Build(Parse("\"resourceGroup\":\"rg-intochat-test\""));

        Assert.All(specs, spec => Assert.Equal("rg-intochat-test", spec.ResourceGroup));
        Assert.Equal("rg-intochat-test", specs.OfType<ResourceGroupSpec>().Single().Name);
        Assert.Equal("rg-intochat-test", InstallPlan.Refs(Parse("\"resourceGroup\":\"rg-intochat-test\"")).ResourceGroup.Name);
    }

    [Fact]
    public async Task The_dry_run_prints_the_configured_resource_group_on_every_line_and_a_summary()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var file = Path.Combine(directory.FullName, "test.json");
            await File.WriteAllTextAsync(file, "{" + Base + ",\"resourceGroup\":\"rg-intochat-test\"}", TestContext.Current.CancellationToken);
            var output = new StringWriter();

            var code = await AzureInstaller.Run(["install", "--env", "test", "--config", file, "--dry-run"], new RecordingAzure(), [], output, new StringWriter(), TestContext.Current.CancellationToken);

            Assert.Equal(0, code);
            var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.All(lines[..^1], line => Assert.EndsWith(" in rg-intochat-test", line, StringComparison.Ordinal));
            Assert.StartsWith("summary: 16 resources in rg-intochat-test (", lines[^1], StringComparison.Ordinal);
            Assert.Contains("role-grant=4", lines[^1], StringComparison.Ordinal);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void Explicit_names_override_the_derived_ones_and_the_rest_keep_the_derived_names()
    {
        var config = Parse("\"names\":{\"os\":\"os-app\",\"identity\":\"id-1\",\"storage\":\"st1\",\"vault\":\"kv-1\",\"environment\":\"env-1\",\"logs\":\"log-1\",\"registry\":\"acr1\"}");

        var specs = InstallPlan.Build(config);

        Assert.Equal("os-app", specs.OfType<ContainerAppSpec>().Single().Name);
        Assert.Equal("id-1", specs.OfType<IdentitySpec>().Single().Name);
        Assert.Equal("st1", specs.OfType<StorageAccountSpec>().Single().Name);
        Assert.Equal("kv-1", specs.OfType<KeyVaultSpec>().Single().Name);
        Assert.Equal("env-1", specs.OfType<ManagedEnvironmentSpec>().Single().Name);
        Assert.Equal("log-1", specs.OfType<LogWorkspaceSpec>().Single().Name);
        Assert.Equal("acr1", specs.OfType<RegistrySpec>().Single().Name);
        Assert.Equal("intochat-test-rg", specs.OfType<ResourceGroupSpec>().Single().Name);
    }

    [Fact]
    public void The_registry_is_a_basic_one_without_an_admin_user_ordered_before_the_vault_and_pulled_by_the_os_identity()
    {
        var specs = InstallPlan.Build(Parse());

        var registry = specs.OfType<RegistrySpec>().Single();
        Assert.Equal("Basic", registry.Sku);
        Assert.Equal("intochattestacr", registry.Name);
        Assert.True(Array.IndexOf(specs, registry) < Array.IndexOf(specs, specs.OfType<KeyVaultSpec>().Single()));
        var pull = specs.OfType<RoleGrantSpec>().Single(grant => grant.Name.EndsWith("registry-pull", StringComparison.Ordinal));
        Assert.Equal("intochat-test-id", pull.Principal);
        Assert.Equal("7f951dda-4ed3-4680-a7ca-43fe172d538d", pull.RoleDefinitionId);
        Assert.EndsWith("/Microsoft.ContainerRegistry/registries/intochattestacr", pull.Scope, StringComparison.Ordinal);
        var foundation = InstallPlan.Refs(Parse());
        Assert.Equal("intochattestacr.azurecr.io", foundation.RegistryServer);
        Assert.Equal("intochattestacr", foundation.Registry?.Name);
    }

    [Fact]
    public void A_registry_server_in_the_config_names_the_registry_the_installer_creates()
    {
        var config = Parse("\"registryServer\":\"acrintochattest.azurecr.io\"");

        Assert.Equal("acrintochattest", InstallPlan.Build(config).OfType<RegistrySpec>().Single().Name);
        Assert.Equal("acrintochattest.azurecr.io", InstallPlan.Refs(config).RegistryServer);
    }

    [Fact]
    public void A_disabled_registry_creates_nothing_and_grants_nothing()
    {
        var specs = InstallPlan.Build(Parse("\"registry\":{\"enabled\":false}"));

        Assert.Empty(specs.OfType<RegistrySpec>());
        Assert.DoesNotContain(specs.OfType<RoleGrantSpec>(), grant => grant.Name.EndsWith("registry-pull", StringComparison.Ordinal));
        Assert.Null(InstallPlan.Refs(Parse("\"registry\":{\"enabled\":false}")).Registry);
    }

    [Fact]
    public void Postgres_is_off_by_default()
    {
        Assert.Empty(InstallPlan.Build(Parse()).OfType<PostgresSpec>());
        Assert.Null(InstallPlan.Refs(Parse()).Postgres);
    }

    [Fact]
    public void Postgres_asks_for_a_burstable_server_a_database_and_one_connection_secret_the_apps_read()
    {
        var config = Parse("\"postgres\":{\"enabled\":true,\"sku\":\"Standard_B1ms\",\"tier\":\"Burstable\",\"storageGb\":64,\"adminLogin\":\"intochat\",\"databases\":[\"intochat\",\"jobs\"]}");

        var specs = InstallPlan.Build(config);

        var server = specs.OfType<PostgresSpec>().Single();
        Assert.Equal(("intochat-test-pg", "Standard_B1ms", "Burstable", 64, "16", "intochat"), (server.Name, server.Sku, server.Tier, server.StorageGb, server.Version, server.AdminLogin));
        Assert.Equal(["intochat", "jobs"], specs.OfType<PostgresDatabaseSpec>().Select(database => database.Name));
        var password = specs.OfType<SecretSpec>().Single(secret => secret.Name == InstallPlan.PostgresPasswordSecret);
        Assert.False(password.Overwrite);
        Assert.Equal(password.Name, server.AdminPasswordSecret);
        var connection = specs.OfType<SecretSpec>().Single(secret => secret.Name == "db-connection");
        Assert.True(connection.Overwrite);
        Assert.Equal("Host=intochat-test-pg.postgres.database.azure.com;Database=intochat;Username=intochat;Password={vault-secret:postgres-admin-password}", connection.Value);
        Assert.Contains(specs.OfType<SecretSpec>(), secret => secret.Name == "db-connection-jobs");
        Assert.True(Array.IndexOf(specs, password) < Array.IndexOf(specs, server));
        Assert.True(Array.IndexOf(specs, server) < Array.IndexOf(specs, connection));
        Assert.Equal("intochat-test-pg.postgres.database.azure.com", InstallPlan.Refs(config).Postgres?.Endpoint);
    }

    [Fact]
    public async Task An_install_generates_the_postgres_password_once_and_uses_the_secrets_file_value_when_given()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var file = Path.Combine(directory.FullName, "test.json");
            await File.WriteAllTextAsync(file, "{" + Base + ",\"postgres\":{\"enabled\":true,\"databases\":[\"intochat\"]}}", TestContext.Current.CancellationToken);
            var secrets = Path.Combine(directory.FullName, "secrets.json");
            await File.WriteAllTextAsync(secrets, "{\"master-key\":\"k\"}", TestContext.Current.CancellationToken);
            var generated = new RecordingAzure();

            Assert.Equal(0, await AzureInstaller.Run(["install", "--env", "test", "--config", file, "--secrets-from", secrets], generated, [], new StringWriter(), new StringWriter(), TestContext.Current.CancellationToken));

            var made = generated.Ensured.OfType<SecretSpec>().Single(secret => secret.Name == InstallPlan.PostgresPasswordSecret);
            Assert.Equal(32, made.Value.Length);
            Assert.False(made.Overwrite);

            await File.WriteAllTextAsync(secrets, "{\"master-key\":\"k\",\"postgres-admin-password\":\"given-password\"}", TestContext.Current.CancellationToken);
            var given = new RecordingAzure();

            Assert.Equal(0, await AzureInstaller.Run(["install", "--env", "test", "--config", file, "--secrets-from", secrets], given, [], new StringWriter(), new StringWriter(), TestContext.Current.CancellationToken));

            Assert.Equal("given-password", given.Ensured.OfType<SecretSpec>().Single(secret => secret.Name == InstallPlan.PostgresPasswordSecret).Value);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task An_install_without_a_master_key_has_the_vault_generate_and_keep_one()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var file = Path.Combine(directory.FullName, "test.json");
            await File.WriteAllTextAsync(file, "{" + Base + "}", TestContext.Current.CancellationToken);
            var azure = new RecordingAzure();

            Assert.Equal(0, await AzureInstaller.Run(["install", "--env", "test", "--config", file], azure, [], new StringWriter(), new StringWriter(), TestContext.Current.CancellationToken));
            var first = azure.Vault[InstallPlan.MasterKeySecret];
            Assert.Equal(0, await AzureInstaller.Run(["install", "--env", "test", "--config", file], azure, [], new StringWriter(), new StringWriter(), TestContext.Current.CancellationToken));

            Assert.Equal(32, Convert.FromBase64String(first).Length);
            Assert.Equal(first, azure.Vault[InstallPlan.MasterKeySecret]);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task The_postgres_block_reads_the_admin_password_from_the_vault_and_writes_db_connection_with_it()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var file = Path.Combine(directory.FullName, "test.json");
            await File.WriteAllTextAsync(file, "{" + Base + ",\"postgres\":{\"enabled\":true,\"adminLogin\":\"intochat\",\"databases\":[\"intochat\"]}}", TestContext.Current.CancellationToken);
            var secrets = Path.Combine(directory.FullName, "secrets.json");
            await File.WriteAllTextAsync(secrets, "{\"master-key\":\"k\",\"postgres-admin-password\":\"pw-1\"}", TestContext.Current.CancellationToken);
            var arm = new RecordingAzure();

            Assert.Equal(0, await AzureInstaller.Run(["install", "--env", "test", "--config", file, "--secrets-from", secrets], arm, [], new StringWriter(), new StringWriter(), TestContext.Current.CancellationToken));

            Assert.Equal(["postgres-admin-password"], arm.VaultReads);
            Assert.Equal("Host=intochat-test-pg.postgres.database.azure.com;Database=intochat;Username=intochat;Password=pw-1", arm.Vault["db-connection"]);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void The_installer_principal_gets_secrets_officer_on_the_vault_right_after_it_is_created()
    {
        var specs = InstallPlan.Build(Parse("\"installerPrincipalId\":\"11111111-1111-1111-1111-111111111111\""));

        var vaultIndex = Array.IndexOf(specs, specs.OfType<KeyVaultSpec>().Single());
        var grant = Assert.IsType<RoleGrantSpec>(specs[vaultIndex + 1]);
        Assert.Equal("11111111-1111-1111-1111-111111111111", grant.Principal);
        // Azure's built-in Key Vault Secrets Officer role (verified against the role catalog).
        Assert.Equal("b86a8fe4-44ce-4948-aee5-eccb2c155cd7", grant.RoleDefinitionId);
        Assert.EndsWith("/Microsoft.KeyVault/vaults/intochat-test-kv", grant.Scope, StringComparison.Ordinal);
        Assert.DoesNotContain(InstallPlan.Build(Parse()).OfType<RoleGrantSpec>(), other => other.Name.EndsWith("installer-secrets", StringComparison.Ordinal));
    }

    [Fact]
    public void A_postgres_config_without_an_admin_login_gets_the_default_and_a_zero_storage_fails()
    {
        Assert.Equal("dbadmin", Parse("\"postgres\":{\"enabled\":true}").Postgres.AdminLogin);
        Assert.Throws<InvalidOperationException>(() => Parse("\"postgres\":{\"enabled\":true,\"storageGb\":0}"));
    }

    [Fact]
    public async Task A_ghcr_user_gives_the_os_app_a_ghcr_credential_and_the_token_secret_is_replaced_only_when_provided()
    {
        var config = Parse("\"ghcrUsername\":\"intochat\"");
        var app = InstallPlan.Build(config).OfType<ContainerAppSpec>().Single();
        var credential = Assert.Single(app.Registries);
        Assert.Equal(("ghcr.io", "intochat", "ghcr-read-token"), (credential.Server, credential.Username, credential.PasswordSecret));

        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var file = Path.Combine(directory.FullName, "test.json");
            await File.WriteAllTextAsync(file, "{" + Base + ",\"ghcrUsername\":\"intochat\"}", TestContext.Current.CancellationToken);
            var secrets = Path.Combine(directory.FullName, "secrets.json");
            await File.WriteAllTextAsync(secrets, "{\"master-key\":\"k\"}", TestContext.Current.CancellationToken);
            var without = new RecordingAzure();
            await AzureInstaller.Run(["install", "--env", "test", "--config", file, "--secrets-from", secrets], without, [], new StringWriter(), new StringWriter(), TestContext.Current.CancellationToken);
            Assert.False(without.Ensured.OfType<SecretSpec>().Single(secret => secret.Name == "ghcr-read-token").Overwrite);

            await File.WriteAllTextAsync(secrets, "{\"master-key\":\"k\",\"ghcr-read-token\":\"ghp_x\"}", TestContext.Current.CancellationToken);
            var with = new RecordingAzure();
            await AzureInstaller.Run(["install", "--env", "test", "--config", file, "--secrets-from", secrets], with, [], new StringWriter(), new StringWriter(), TestContext.Current.CancellationToken);
            var token = with.Ensured.OfType<SecretSpec>().Single(secret => secret.Name == "ghcr-read-token");
            Assert.True(token.Overwrite);
            Assert.Equal("ghp_x", token.Value);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task A_container_app_with_internal_ingress_secret_files_and_registries_passes_through_the_neuron_unchanged()
    {
        var arm = new RecordingAzure();
        var neuron = AzureNeurons.Create(arm, "s");
        var spec = new ContainerAppSpec
        {
            Name = "intochat-test-os-ui",
            ResourceGroup = "rg-intochat-test",
            Location = "polandcentral",
            Env = "test",
            Identity = "intochat-test-apps-id",
            Ingress = new IngressSpec { External = false, TargetPort = 80 },
            SecretFiles = [new SecretFileSpec { Name = "ui-config", Vault = "kv", VaultSecret = "ui-config", Path = "/usr/share/nginx/html/config.json" }],
            Registries = [new RegistryCredentialSpec { Server = "ghcr.io", Username = "intochat", Vault = "kv", PasswordSecret = "ghcr-read-token" }, new RegistryCredentialSpec { Server = "acr.azurecr.io", Identity = "intochat-test-apps-id" }],
        };

        await neuron.Ensure(spec);

        var recorded = Assert.IsType<ContainerAppSpec>(Assert.Single(arm.Ensured));
        Assert.False(recorded.Ingress.External);
        Assert.Equal(80, recorded.Ingress.TargetPort);
        Assert.Equal("/usr/share/nginx/html/config.json", Assert.Single(recorded.SecretFiles).Path);
        Assert.Equal(2, recorded.Registries.Length);
        Assert.Equal("intochat-test-apps-id", recorded.Identity);
    }

    [Fact]
    public void The_resource_ids_of_the_new_kinds_follow_their_providers()
    {
        var registry = ResourceIds.For("s", ResourceKinds.Registry, "rg", "acr");
        var database = ResourceIds.For("s", new PostgresDatabaseSpec { Name = "db", Server = "pg", ResourceGroup = "rg" });

        Assert.Equal("/subscriptions/s/resourceGroups/rg/providers/Microsoft.ContainerRegistry/registries/acr", registry);
        Assert.Equal("/subscriptions/s/resourceGroups/rg/providers/Microsoft.DBforPostgreSQL/flexibleServers/pg/databases/db", database);
    }
}
