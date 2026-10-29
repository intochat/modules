using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Core.Pipeline;

namespace IntoChat.OS.Modules.Microsoft.Azure.Tests.Unit;

public sealed class ArmRepeatabilityFacts
{
    [Fact]
    public async Task Session_pool_ensure_preserves_the_management_endpoint_reported_by_azure()
    {
        using var handler = new AzureHttp();
        using var client = new HttpClient(handler);
        var arm = new ArmAzure(new Credential(), new HttpClientTransport(client));

        var made = await arm.Ensure(AzureHttp.Subscription, new SessionPoolSpec
        {
            Name = "test-sandbox",
            ResourceGroup = "rg",
            Location = "polandcentral",
            Environment = "test-env",
            Identity = "test-id",
            Image = "acr.azurecr.io/sandbox:1",
            RegistryServer = "acr.azurecr.io",
        }, TestContext.Current.CancellationToken);

        Assert.Equal("https://test-sandbox.env-123.polandcentral.azurecontainerapps.io/", made.Endpoint);
    }

    [Fact]
    public async Task Vault_without_explicit_tenant_loads_the_subscription_before_using_its_tenant()
    {
        using var handler = new AzureHttp();
        using var client = new HttpClient(handler);
        var arm = new ArmAzure(new Credential(), new HttpClientTransport(client));

        var made = await arm.Ensure(AzureHttp.Subscription,
            new KeyVaultSpec { Name = "test-kv", ResourceGroup = "rg", Location = "polandcentral" }, TestContext.Current.CancellationToken);

        Assert.Equal(1, handler.SubscriptionReads);
        Assert.Equal(AzureHttp.SubscriptionTenant, handler.LastVaultWrite?["properties"]?["tenantId"]?.GetValue<string>());
        Assert.True(handler.LastVaultWrite?["properties"]?["enableRbacAuthorization"]?.GetValue<bool>());
        Assert.EndsWith("/providers/Microsoft.KeyVault/vaults/test-kv", made.Id, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vault_with_explicit_tenant_does_not_need_to_read_the_subscription()
    {
        const string explicitTenant = "00000000-0000-0000-0000-000000000003";
        using var handler = new AzureHttp();
        using var client = new HttpClient(handler);
        var arm = new ArmAzure(new Credential(), new HttpClientTransport(client));

        await arm.Ensure(AzureHttp.Subscription,
            new KeyVaultSpec { Name = "test-kv", ResourceGroup = "rg", Location = "polandcentral", TenantId = explicitTenant }, TestContext.Current.CancellationToken);

        Assert.Equal(0, handler.SubscriptionReads);
        Assert.Equal(explicitTenant, handler.LastVaultWrite?["properties"]?["tenantId"]?.GetValue<string>());
    }

    [Fact]
    public async Task Vault_without_either_tenant_fails_before_the_vault_write()
    {
        using var handler = new AzureHttp { Tenant = null };
        using var client = new HttpClient(handler);
        var arm = new ArmAzure(new Credential(), new HttpClientTransport(client));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => arm.Ensure(AzureHttp.Subscription,
            new KeyVaultSpec { Name = "test-kv", ResourceGroup = "rg", Location = "polandcentral" }, TestContext.Current.CancellationToken));

        Assert.Contains("subscription has no tenant id", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, handler.SubscriptionReads);
        Assert.Null(handler.LastVaultWrite);
    }

    [Fact]
    public async Task Identical_secret_does_not_write_another_version_but_a_changed_value_does()
    {
        using var handler = new AzureHttp();
        using var client = new HttpClient(handler);
        var arm = new ArmAzure(new Credential(), new HttpClientTransport(client));
        var secret = new SecretSpec { Name = "google-secret", Vault = "test-kv", Value = "first-value", Overwrite = true, ResourceGroup = "rg", Location = "polandcentral" };

        await arm.Ensure(AzureHttp.Subscription, secret, TestContext.Current.CancellationToken);
        await arm.Ensure(AzureHttp.Subscription, secret, TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.SecretWrites);

        await arm.Ensure(AzureHttp.Subscription, secret with { Value = "second-value" }, TestContext.Current.CancellationToken);
        Assert.Equal(2, handler.SecretWrites);
        await arm.Ensure(AzureHttp.Subscription, secret with { Value = "third-value", Overwrite = false }, TestContext.Current.CancellationToken);
        Assert.Equal(2, handler.SecretWrites);
    }

    [Fact]
    public async Task Generated_secret_is_created_when_absent_and_never_replaced()
    {
        using var handler = new AzureHttp();
        using var client = new HttpClient(handler);
        var arm = new ArmAzure(new Credential(), new HttpClientTransport(client));
        var spec = new SecretSpec { Name = "google-secret", Vault = "test-kv", Generator = SecretGenerators.Base64(32), ResourceGroup = "rg", Location = "polandcentral" };

        Assert.Null(await arm.ReadSecret("test-kv", "google-secret", TestContext.Current.CancellationToken));
        await arm.Ensure(AzureHttp.Subscription, spec, TestContext.Current.CancellationToken);
        var first = await arm.ReadSecret("test-kv", "google-secret", TestContext.Current.CancellationToken);
        await arm.Ensure(AzureHttp.Subscription, spec, TestContext.Current.CancellationToken);

        Assert.Equal(1, handler.SecretWrites);
        Assert.Equal(32, Convert.FromBase64String(first!).Length);
        Assert.Equal(first, await arm.ReadSecret("test-kv", "google-secret", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Generated_secret_keeps_a_value_an_earlier_install_wrote()
    {
        using var handler = new AzureHttp();
        using var client = new HttpClient(handler);
        var arm = new ArmAzure(new Credential(), new HttpClientTransport(client));
        var spec = new SecretSpec { Name = "google-secret", Vault = "test-kv", ResourceGroup = "rg", Location = "polandcentral" };
        await arm.Ensure(AzureHttp.Subscription, spec with { Value = "existing", Overwrite = true }, TestContext.Current.CancellationToken);

        await arm.Ensure(AzureHttp.Subscription, spec with { Generator = SecretGenerators.P256Pkcs8 }, TestContext.Current.CancellationToken);

        Assert.Equal(1, handler.SecretWrites);
        Assert.Equal("existing", await arm.ReadSecret("test-kv", "google-secret", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task External_secret_is_read_only_and_a_missing_one_names_the_command_to_set_it()
    {
        using var handler = new AzureHttp();
        using var client = new HttpClient(handler);
        var arm = new ArmAzure(new Credential(), new HttpClientTransport(client));
        var spec = new SecretSpec { Name = "google-secret", Vault = "test-kv", External = true, Value = "ignored", Overwrite = true, ResourceGroup = "rg", Location = "polandcentral" };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => arm.Ensure(AzureHttp.Subscription, spec, TestContext.Current.CancellationToken));
        Assert.Contains("az keyvault secret set --vault-name test-kv --name google-secret", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, handler.SecretWrites);

        await arm.Ensure(AzureHttp.Subscription, spec with { External = false, Value = "set-by-operator" }, TestContext.Current.CancellationToken);
        await arm.Ensure(AzureHttp.Subscription, spec, TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.SecretWrites);
        Assert.Equal("set-by-operator", await arm.ReadSecret("test-kv", "google-secret", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Generators_make_random_bytes_and_a_signing_key_whose_public_key_derives()
    {
        Assert.Equal(48, Convert.FromBase64String(SecretGenerators.Generate(SecretGenerators.Base64(48))).Length);
        Assert.NotEqual(SecretGenerators.Generate(SecretGenerators.Base64(32)), SecretGenerators.Generate(SecretGenerators.Base64(32)));
        var key = SecretGenerators.Generate(SecretGenerators.P256Pkcs8);
        using var ecdsa = System.Security.Cryptography.ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(SecretGenerators.P256PublicKey(key)), out _);
        Assert.Equal(256, ecdsa.KeySize);
        Assert.Throws<ArgumentException>(() => SecretGenerators.Generate("base64:0"));
        Assert.Throws<ArgumentException>(() => SecretGenerators.Generate("rot13"));
    }

    [Fact]
    public async Task Identical_container_template_does_not_send_another_update_but_a_changed_image_does()
    {
        using var handler = new AzureHttp();
        using var client = new HttpClient(handler);
        var arm = new ArmAzure(new Credential(), new HttpClientTransport(client));
        var app = new ContainerAppSpec
        {
            Name = "test-api",
            ResourceGroup = "rg",
            Location = "polandcentral",
            Environment = "test-env",
            Identity = "test-id",
            Image = "acr.azurecr.io/api:1",
            Variables = new Dictionary<string, string> { ["B"] = "second", ["A"] = "first" },
        };

        await arm.Ensure(AzureHttp.Subscription, app, TestContext.Current.CancellationToken);
        await arm.Ensure(AzureHttp.Subscription, app with { Variables = new Dictionary<string, string> { ["A"] = "first", ["B"] = "second" } }, TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.AppWrites);

        await arm.Ensure(AzureHttp.Subscription, app with { Image = "acr.azurecr.io/api:2" }, TestContext.Current.CancellationToken);
        Assert.Equal(2, handler.AppWrites);
        await arm.Ensure(AzureHttp.Subscription, app with { Image = "acr.azurecr.io/api:2", Variables = new Dictionary<string, string>() }, TestContext.Current.CancellationToken);
        Assert.Equal(3, handler.AppWrites);
    }

    [Fact]
    public async Task Updating_image_preserves_existing_custom_domain_and_certificate_binding()
    {
        using var handler = new AzureHttp();
        using var client = new HttpClient(handler);
        var arm = new ArmAzure(new Credential(), new HttpClientTransport(client));
        var app = new ContainerAppSpec
        {
            Name = "test-api",
            ResourceGroup = "rg",
            Location = "polandcentral",
            Environment = "test-env",
            Identity = "test-id",
            Image = "acr.azurecr.io/api:1",
        };
        await arm.Ensure(AzureHttp.Subscription, app, TestContext.Current.CancellationToken);
        handler.BindCustomDomain();

        await arm.Ensure(AzureHttp.Subscription, app, TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.AppWrites);
        await arm.Ensure(AzureHttp.Subscription, app with { Image = "acr.azurecr.io/api:2" }, TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.AppWrites);
        var domains = handler.LastAppWrite?["properties"]?["configuration"]?["ingress"]?["customDomains"];
        Assert.NotNull(domains);
        Assert.Equal("api.test.intochat.io", domains[0]?["name"]?.GetValue<string>());
        Assert.Equal("SniEnabled", domains[0]?["bindingType"]?.GetValue<string>());
        Assert.Equal(AzureHttp.CertificateId, domains[0]?["certificateId"]?.GetValue<string>());

        await arm.Ensure(AzureHttp.Subscription, app with { Image = "acr.azurecr.io/api:2" }, TestContext.Current.CancellationToken);
        Assert.Equal(2, handler.AppWrites);
    }

    private sealed class Credential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("offline-test-token", DateTimeOffset.UtcNow.AddHours(1));
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    // Azure SDK requests cross a real HTTP transport; only the remote service is replaced.
    private sealed class AzureHttp : HttpMessageHandler
    {
        public const string Subscription = "00000000-0000-0000-0000-000000000001";
        public const string SubscriptionTenant = "00000000-0000-0000-0000-000000000002";
        private const string Group = "/subscriptions/" + Subscription + "/resourceGroups/rg";
        public const string CertificateId = Group + "/providers/Microsoft.App/managedEnvironments/test-env/managedCertificates/test-cert";
        private JsonObject? _app;
        private string? _secret;
        public int SecretWrites { get; private set; }
        public int AppWrites { get; private set; }
        public JsonObject? LastAppWrite => _app;
        public int SubscriptionReads { get; private set; }
        public string? Tenant { get; init; } = SubscriptionTenant;
        public JsonObject? LastVaultWrite { get; private set; }

        public void BindCustomDomain()
        {
            _app!["properties"]!["configuration"]!["ingress"]!["customDomains"] = new JsonArray
            {
                new JsonObject { ["name"] = "api.test.intochat.io", ["bindingType"] = "SniEnabled", ["certificateId"] = CertificateId },
            };
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var path = uri.AbsolutePath;
            JsonObject? result;
            if (uri.Host == "test-kv.vault.azure.net")
            {
                if (request.Headers.Authorization is null)
                {
                    var challenge = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                    challenge.Headers.TryAddWithoutValidation("WWW-Authenticate", "Bearer authorization=\"https://login.windows.net/00000000-0000-0000-0000-000000000001\", resource=\"https://vault.azure.net\"");
                    return challenge;
                }
                if (request.Method == HttpMethod.Put)
                {
                    SecretWrites++;
                    _secret = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!["value"]!.GetValue<string>();
                }
                result = _secret is null ? null : new JsonObject { ["value"] = _secret, ["id"] = "https://test-kv.vault.azure.net/secrets/google-secret/version", ["attributes"] = new JsonObject { ["enabled"] = true } };
            }
            else if (path.Equals("/subscriptions/" + Subscription, StringComparison.OrdinalIgnoreCase))
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                SubscriptionReads++;
                result = new JsonObject { ["id"] = "/subscriptions/" + Subscription, ["subscriptionId"] = Subscription, ["displayName"] = "Offline test", ["state"] = "Enabled" };
                if (Tenant is not null) { result["tenantId"] = Tenant; }
            }
            else if (path.Equals(Group + "/providers/Microsoft.KeyVault/vaults/test-kv", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Equal(HttpMethod.Put, request.Method);
                LastVaultWrite = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
                result = LastVaultWrite.DeepClone().AsObject();
                result["id"] = Group + "/providers/Microsoft.KeyVault/vaults/test-kv";
                result["name"] = "test-kv";
                result["properties"]!["provisioningState"] = "Succeeded";
                result["properties"]!["vaultUri"] = "https://test-kv.vault.azure.net/";
            }
            else if (path.Equals(Group, StringComparison.OrdinalIgnoreCase))
            {
                result = new JsonObject { ["id"] = Group, ["name"] = "rg", ["location"] = "polandcentral", ["properties"] = new JsonObject { ["provisioningState"] = "Succeeded" } };
            }
            else if (path.Contains("/managedEnvironments/", StringComparison.OrdinalIgnoreCase))
            {
                result = new JsonObject { ["id"] = Group + "/providers/Microsoft.App/managedEnvironments/test-env", ["name"] = "test-env", ["location"] = "polandcentral", ["properties"] = new JsonObject { ["provisioningState"] = "Succeeded" } };
            }
            else if (path.Contains("/userAssignedIdentities/", StringComparison.OrdinalIgnoreCase))
            {
                result = new JsonObject { ["id"] = Group + "/providers/Microsoft.ManagedIdentity/userAssignedIdentities/test-id", ["name"] = "test-id", ["location"] = "polandcentral", ["properties"] = new JsonObject { ["clientId"] = Subscription, ["principalId"] = Subscription } };
            }
            else if (path.Contains("/containerApps/", StringComparison.OrdinalIgnoreCase))
            {
                if (request.Method == HttpMethod.Put)
                {
                    AppWrites++;
                    _app = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
                    _app["id"] = Group + "/providers/Microsoft.App/containerApps/test-api";
                    _app["name"] = "test-api";
                    _app["properties"]!["provisioningState"] = "Succeeded";
                }
                result = _app;
            }
            else if (path.Equals(Group + "/providers/Microsoft.App/sessionPools/test-sandbox", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Equal(HttpMethod.Put, request.Method);
                result = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
                result["id"] = path;
                result["name"] = "test-sandbox";
                result["properties"]!["provisioningState"] = "Succeeded";
                result["properties"]!["poolManagementEndpoint"] = "https://test-sandbox.env-123.polandcentral.azurecontainerapps.io/";
            }
            else { throw new InvalidOperationException($"Unexpected Azure test request: {request.Method} {uri.GetLeftPart(UriPartial.Path)}"); }

            return new HttpResponseMessage(result is null ? HttpStatusCode.NotFound : HttpStatusCode.OK)
            {
                Content = new StringContent(result?.ToJsonString() ?? "{\"error\":{\"code\":\"NotFound\",\"message\":\"not found\"}}", Encoding.UTF8, "application/json"),
            };
        }
    }
}
