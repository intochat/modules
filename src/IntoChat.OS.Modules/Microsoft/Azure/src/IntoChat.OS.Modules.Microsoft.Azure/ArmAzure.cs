using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.ClientModel.Primitives;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.Authorization;
using Azure.ResourceManager.Authorization.Models;
using Azure.ResourceManager.ContainerRegistry;
using Azure.ResourceManager.ContainerRegistry.Models;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.KeyVault.Models;
using Azure.ResourceManager.ManagedServiceIdentities;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.OperationalInsights;
using Azure.ResourceManager.OperationalInsights.Models;
using Azure.ResourceManager.PostgreSql.FlexibleServers;
using Azure.ResourceManager.PostgreSql.FlexibleServers.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;

using AzureSdk = Azure;
namespace IntoChat.OS.Modules.Microsoft.Azure;

// The real calls: one idempotent CreateOrUpdate (a PUT) per kind, under DefaultAzureCredential.
// HTTP transport facts cover secret and Container App repeatability without a live Azure subscription.
public sealed class ArmAzure : IAzureArm
{
    private readonly TokenCredential _credential;
    private readonly HttpPipelineTransport? _transport;
    private readonly Lazy<ArmClient> _client;

    public ArmAzure() : this(new DefaultAzureCredential(), null) { }

    internal ArmAzure(TokenCredential credential, HttpPipelineTransport? transport)
    {
        _credential = credential;
        _transport = transport;
        _client = new(() => new ArmClient(_credential, null, transport is null ? null : new ArmClientOptions { Transport = transport }));
    }

    public async Task<ResourceRef> Ensure(string subscription, ResourceSpec spec, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var sub = Subscription(subscription);
        var location = new AzureLocation(spec.Location);
        if (spec is ResourceGroupSpec group)
        {
            var data = new ResourceGroupData(location);
            Apply(spec, data.Tags);
            var made = await sub.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, group.Name, data, ct);
            return Ref(made.Value.Id, spec, group.Name);
        }

        var rg = (await sub.GetResourceGroupAsync(spec.ResourceGroup, ct)).Value;
        return spec switch
        {
            IdentitySpec identity => await EnsureIdentity(rg, identity, location, ct),
            LogWorkspaceSpec workspace => await EnsureWorkspace(rg, workspace, location, ct),
            StorageAccountSpec account => await EnsureStorage(rg, account, location, ct),
            FileShareSpec share => await EnsureShare(rg, share, ct),
            BlobContainerSpec container => await EnsureContainer(rg, container, ct),
            KeyVaultSpec vault => await EnsureVault(sub, rg, vault, location, ct),
            SecretSpec secret => await EnsureSecret(secret, ct),
            ManagedEnvironmentSpec environment => await EnsureEnvironment(rg, environment, location, ct),
            ContainerAppSpec app => await EnsureApp(rg, app, location, ct),
            RoleGrantSpec grant => await EnsureGrant(rg, grant, ct),
            NetworkSecurityGroupSpec securityGroup => await EnsureNetworkSecurityGroup(rg, securityGroup, location, ct),
            VirtualNetworkSpec network => await EnsureVirtualNetwork(rg, network, location, ct),
            SubnetSpec subnet => await EnsureSubnet(rg, subnet, ct),
            SessionPoolSpec pool => await EnsureSessionPool(rg, pool, location, ct),
            RegistrySpec registry => await EnsureRegistry(rg, registry, location, ct),
            PostgresSpec postgres => await EnsurePostgres(rg, postgres, location, ct),
            PostgresDatabaseSpec database => await EnsurePostgresDatabase(rg, database, ct),
            _ => throw new NotSupportedException($"Azure resource kind '{spec.Kind}' is not supported."),
        };
    }

    public async Task<ResourceRef?> Read(string subscription, ResourceRef resource, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resource);
        try
        {
            var generic = Client.GetGenericResource(new ResourceIdentifier(resource.Id));
            var found = await generic.GetAsync(ct);
            return resource with { Id = found.Value.Id.ToString() };
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
    }

    public async Task Remove(string subscription, ResourceRef resource, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resource);
        try { await Client.GetGenericResource(new ResourceIdentifier(resource.Id)).DeleteAsync(WaitUntil.Completed, ct); }
        catch (RequestFailedException ex) when (ex.Status == 404) { return; } // already gone
    }

    public async Task<ResourceRef[]> List(string subscription, string tagValue, CancellationToken ct)
    {
        var found = new List<ResourceRef>();
        var filter = $"tagName eq '{AzureTags.ManagedBy}' and tagValue eq '{AzureTags.ManagedByValue}'";
        await foreach (var resource in Subscription(subscription).GetGenericResourcesAsync(filter, cancellationToken: ct))
        {
            var tags = resource.Data.Tags;
            if (!(tags.TryGetValue(AzureTags.Env, out var env) && env == tagValue) && !(tags.TryGetValue(AzureTags.Owner, out var owner) && owner == tagValue)) { continue; }
            found.Add(new ResourceRef(resource.Id.ToString(), resource.Data.ResourceType.ToString(), resource.Id.Name, resource.Id.ResourceGroupName ?? ""));
        }
        return [.. found];
    }

    private ArmClient Client => _client.Value;

    private SubscriptionResource Subscription(string subscription) =>
        Client.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscription));

    private static ResourceRef Ref(ResourceIdentifier id, ResourceSpec spec, string name) =>
        new(id.ToString(), spec.Kind, name, spec.ResourceGroup);

    private static void Apply(ResourceSpec spec, IDictionary<string, string> tags)
    {
        foreach (var (name, value) in AzureTags.Merge(spec)) { tags[name] = value; }
    }

    private static async Task<ResourceRef> EnsureIdentity(ResourceGroupResource rg, IdentitySpec spec, AzureLocation location, CancellationToken ct)
    {
        var data = new UserAssignedIdentityData(location);
        Apply(spec, data.Tags);
        var made = await rg.GetUserAssignedIdentities().CreateOrUpdateAsync(WaitUntil.Completed, spec.Name, data, ct);
        return Ref(made.Value.Id, spec, spec.Name);
    }

    private static async Task<ResourceRef> EnsureWorkspace(ResourceGroupResource rg, LogWorkspaceSpec spec, AzureLocation location, CancellationToken ct)
    {
        var data = new OperationalInsightsWorkspaceData(location)
        {
            Sku = new OperationalInsightsWorkspaceSku(OperationalInsightsWorkspaceSkuName.PerGB2018),
            RetentionInDays = spec.RetentionDays,
        };
        Apply(spec, data.Tags);
        var made = await rg.GetOperationalInsightsWorkspaces().CreateOrUpdateAsync(WaitUntil.Completed, spec.Name, data, ct);
        return Ref(made.Value.Id, spec, spec.Name);
    }

    private static async Task<ResourceRef> EnsureStorage(ResourceGroupResource rg, StorageAccountSpec spec, AzureLocation location, CancellationToken ct)
    {
        var content = new StorageAccountCreateOrUpdateContent(new StorageSku(new StorageSkuName(spec.Sku)), StorageKind.StorageV2, location)
        {
            MinimumTlsVersion = StorageMinimumTlsVersion.Tls1_2,
            AllowBlobPublicAccess = false,
        };
        Apply(spec, content.Tags);
        var made = await rg.GetStorageAccounts().CreateOrUpdateAsync(WaitUntil.Completed, spec.Name, content, ct);
        return Ref(made.Value.Id, spec, spec.Name);
    }

    private static async Task<ResourceRef> EnsureShare(ResourceGroupResource rg, FileShareSpec spec, CancellationToken ct)
    {
        var account = (await rg.GetStorageAccountAsync(spec.StorageAccount, cancellationToken: ct)).Value;
        var made = await account.GetFileService().GetFileShares()
            .CreateOrUpdateAsync(WaitUntil.Completed, spec.Name, new FileShareData { ShareQuota = spec.QuotaGb }, cancellationToken: ct);
        return Ref(made.Value.Id, spec, spec.Name);
    }

    private static async Task<ResourceRef> EnsureContainer(ResourceGroupResource rg, BlobContainerSpec spec, CancellationToken ct)
    {
        var account = (await rg.GetStorageAccountAsync(spec.StorageAccount, cancellationToken: ct)).Value;
        var made = await account.GetBlobService().GetBlobContainers()
            .CreateOrUpdateAsync(WaitUntil.Completed, spec.Name, new BlobContainerData { PublicAccess = StoragePublicAccessType.None }, ct);
        return Ref(made.Value.Id, spec, spec.Name);
    }

    private static async Task<ResourceRef> EnsureVault(SubscriptionResource sub, ResourceGroupResource rg, KeyVaultSpec spec, AzureLocation location, CancellationToken ct)
    {
        var tenant = Guid.TryParse(spec.TenantId, out var given)
            ? given
            : (await sub.GetAsync(ct)).Value.Data.TenantId ?? throw new InvalidOperationException("The subscription has no tenant id.");
        var properties = new AzureSdk.ResourceManager.KeyVault.Models.KeyVaultProperties(tenant, new KeyVaultSku(KeyVaultSkuFamily.A, KeyVaultSkuName.Standard)) { EnableRbacAuthorization = true };
        var content = new KeyVaultCreateOrUpdateContent(location, properties);
        Apply(spec, content.Tags);
        var made = await rg.GetKeyVaults().CreateOrUpdateAsync(WaitUntil.Completed, spec.Name, content, ct);
        return Ref(made.Value.Id, spec, spec.Name);
    }

    // Secret values are a data-plane matter: the management plane never returns them. The installer principal needs
    // Key Vault Secrets Officer on the vault (InstallPlan grants it from installerPrincipalId).
    private SecretClient Secrets(string vault) => new(new Uri($"https://{vault}.vault.azure.net/"), _credential,
        _transport is null ? null : new SecretClientOptions { Transport = _transport });

    public async Task<string?> ReadSecret(string vault, string name, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vault);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return (await FindSecret(Secrets(vault), name, ct))?.Value;
    }

    private static async Task<KeyVaultSecret?> FindSecret(SecretClient client, string name, CancellationToken ct)
    {
        try { return (await client.GetSecretAsync(name, cancellationToken: ct)).Value; }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
    }

    private async Task<ResourceRef> EnsureSecret(SecretSpec spec, CancellationToken ct)
    {
        var client = Secrets(spec.Vault);
        var existing = await FindSecret(client, spec.Name, ct);
        if (spec.External)
        {
            return existing is { Value.Length: > 0 }
                ? SecretRef(spec, existing)
                : throw new InvalidOperationException(
                    $"The external secret '{spec.Name}' is not set in vault '{spec.Vault}'. Set it once with: az keyvault secret set --vault-name {spec.Vault} --name {spec.Name} --value <value>");
        }
        if (spec.Generator.Length > 0)
        {
            if (existing is not null) { return SecretRef(spec, existing); }
            var generated = await client.SetSecretAsync(spec.Name, SecretGenerators.Generate(spec.Generator), ct);
            return SecretRef(spec, generated.Value);
        }
        if (!spec.Overwrite && existing is not null)
        {
            return SecretRef(spec, existing);
        }
        var referenced = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in SecretSpec.ReferencesIn(spec.Value))
        {
            referenced[name] = (await client.GetSecretAsync(name, cancellationToken: ct)).Value.Value;
        }
        var value = SecretSpec.Substitute(spec.Value, referenced);
        if (existing is not null && string.Equals(existing.Value, value, StringComparison.Ordinal)) { return SecretRef(spec, existing); }
        var made = await client.SetSecretAsync(spec.Name, value, ct);
        return SecretRef(spec, made.Value);
    }

    private static ResourceRef SecretRef(SecretSpec spec, KeyVaultSecret secret) =>
        new(secret.Id.ToString(), spec.Kind, spec.Name, spec.ResourceGroup);

    private static async Task<ResourceRef> EnsureRegistry(ResourceGroupResource rg, RegistrySpec spec, AzureLocation location, CancellationToken ct)
    {
        var data = new ContainerRegistryData(location, new ContainerRegistrySku(new ContainerRegistrySkuName(spec.Sku))) { IsAdminUserEnabled = false };
        Apply(spec, data.Tags);
        var made = await rg.GetContainerRegistries().CreateOrUpdateAsync(WaitUntil.Completed, spec.Name, data, ct);
        return Ref(made.Value.Id, spec, spec.Name) with { Endpoint = made.Value.Data.LoginServer ?? "" };
    }

    private async Task<ResourceRef> EnsurePostgres(ResourceGroupResource rg, PostgresSpec spec, AzureLocation location, CancellationToken ct)
    {
        var password = (await Secrets(spec.Vault).GetSecretAsync(spec.AdminPasswordSecret, cancellationToken: ct)).Value.Value
            ?? throw new InvalidOperationException($"Secret {spec.AdminPasswordSecret} has no value for the Postgres admin password.");
        var data = new PostgreSqlFlexibleServerData(location)
        {
            Sku = new PostgreSqlFlexibleServerSku(spec.Sku, new PostgreSqlFlexibleServerSkuTier(spec.Tier)),
            Version = new PostgreSqlFlexibleServerVersion(spec.Version),
            AdministratorLogin = spec.AdminLogin,
            AdministratorLoginPassword = password,
            Storage = new PostgreSqlFlexibleServerStorage { StorageSizeInGB = spec.StorageGb },
            Network = new PostgreSqlFlexibleServerNetwork { PublicNetworkAccess = PostgreSqlFlexibleServerPublicNetworkAccessState.Enabled },
        };
        Apply(spec, data.Tags);
        var made = await rg.GetPostgreSqlFlexibleServers().CreateOrUpdateAsync(WaitUntil.Completed, spec.Name, data, ct);
        // 0.0.0.0 to 0.0.0.0 is the allow-Azure-services rule.
        await made.Value.GetPostgreSqlFlexibleServerFirewallRules().CreateOrUpdateAsync(
            WaitUntil.Completed,
            "AllowAllAzureServicesAndResourcesWithinAzureIps",
            new PostgreSqlFlexibleServerFirewallRuleData(IPAddress.Any, IPAddress.Any),
            ct);
        return Ref(made.Value.Id, spec, spec.Name) with { Endpoint = made.Value.Data.FullyQualifiedDomainName ?? "" };
    }

    private static async Task<ResourceRef> EnsurePostgresDatabase(ResourceGroupResource rg, PostgresDatabaseSpec spec, CancellationToken ct)
    {
        var server = (await rg.GetPostgreSqlFlexibleServerAsync(spec.Server, ct)).Value;
        var made = await server.GetPostgreSqlFlexibleServerDatabases().CreateOrUpdateAsync(WaitUntil.Completed, spec.Name, new PostgreSqlFlexibleServerDatabaseData(), ct);
        return Ref(made.Value.Id, spec, spec.Name);
    }

    private static async Task<ResourceRef> EnsureEnvironment(ResourceGroupResource rg, ManagedEnvironmentSpec spec, AzureLocation location, CancellationToken ct)
    {
        var workspace = (await rg.GetOperationalInsightsWorkspaceAsync(spec.LogWorkspace, ct)).Value;
        var keys = (await workspace.GetSharedKeysAsync(ct)).Value;
        var data = new ContainerAppManagedEnvironmentData(location)
        {
            AppLogsConfiguration = new ContainerAppLogsConfiguration
            {
                Destination = "log-analytics",
                LogAnalyticsConfiguration = new ContainerAppLogAnalyticsConfiguration
                {
                    CustomerId = workspace.Data.CustomerId?.ToString(),
                    SharedKey = keys.PrimarySharedKey,
                },
            },
        };
        data.WorkloadProfiles.Add(new ContainerAppWorkloadProfile("Consumption", "Consumption"));
        if (spec.SubnetId is { Length: > 0 } subnet)
        {
            data.VnetConfiguration = new ContainerAppVnetConfiguration { InfrastructureSubnetId = new ResourceIdentifier(subnet) };
        }
        Apply(spec, data.Tags);
        var made = await rg.GetContainerAppManagedEnvironments().CreateOrUpdateAsync(WaitUntil.Completed, spec.Name, data, ct);
        foreach (var storage in spec.Storages)
        {
            var account = (await rg.GetStorageAccountAsync(storage.StorageAccount, cancellationToken: ct)).Value;
            var accountKey = (await account.GetKeysAsync(cancellationToken: ct)).Value.Keys[0].Value;
            var properties = new ContainerAppManagedEnvironmentStorageData
            {
                Properties = new ManagedEnvironmentStorageProperties
                {
                    AzureFile = new ContainerAppAzureFileProperties
                    {
                        AccountName = storage.StorageAccount,
                        AccountKey = accountKey,
                        ShareName = storage.ShareName,
                        AccessMode = ContainerAppAccessMode.ReadWrite,
                    },
                },
            };
            await made.Value.GetContainerAppManagedEnvironmentStorages().CreateOrUpdateAsync(WaitUntil.Completed, storage.Name, properties, ct);
        }
        return Ref(made.Value.Id, spec, spec.Name);
    }

    private static async Task<ResourceRef> EnsureApp(ResourceGroupResource rg, ContainerAppSpec spec, AzureLocation location, CancellationToken ct)
    {
        var environment = (await rg.GetContainerAppManagedEnvironmentAsync(spec.Environment, ct)).Value;
        var identity = (await rg.GetUserAssignedIdentityAsync(spec.Identity, ct)).Value;
        var identityId = identity.Id.ToString();
        var clientId = identity.Data.ClientId?.ToString() ?? "";

        var container = new ContainerAppContainer
        {
            Name = "app",
            Image = spec.Image,
            Resources = new AppContainerResources { Cpu = spec.Cpu, Memory = spec.Memory },
        };
        foreach (var (name, value) in spec.Variables.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            container.Env.Add(new ContainerAppEnvironmentVariable { Name = name, Value = value.Replace(ContainerAppSpec.IdentityClientIdToken, clientId, StringComparison.Ordinal) });
        }
        foreach (var secret in spec.Secrets)
        {
            container.Env.Add(new ContainerAppEnvironmentVariable { Name = secret.Variable, SecretRef = secret.Name });
        }
        foreach (var mount in spec.VolumeMounts)
        {
            container.VolumeMounts.Add(new ContainerAppVolumeMount { VolumeName = mount.Volume, MountPath = mount.MountPath });
        }
        foreach (var file in spec.SecretFiles)
        {
            // A sub-path mount puts the one file at Path without hiding the rest of its directory.
            container.VolumeMounts.Add(new ContainerAppVolumeMount { VolumeName = SecretVolume(file), MountPath = file.Path, SubPath = FileName(file) });
        }
        var targetPort = spec.Ingress.TargetPort > 0 ? spec.Ingress.TargetPort : spec.IngressPort;
        var probeTypes = new[] { ContainerAppProbeType.Liveness, ContainerAppProbeType.Readiness, ContainerAppProbeType.Startup };
        for (var i = 0; i < spec.ProbePaths.Length && i < probeTypes.Length; i++)
        {
            container.Probes.Add(new ContainerAppProbe
            {
                ProbeType = probeTypes[i],
                HttpGet = new ContainerAppHttpRequestInfo(targetPort) { Path = spec.ProbePaths[i] },
                FailureThreshold = probeTypes[i] == ContainerAppProbeType.Startup ? 30 : 3,
                PeriodSeconds = 10,
            });
        }

        var template = new ContainerAppTemplate
        {
            TerminationGracePeriodSeconds = spec.TerminationGracePeriodSeconds,
            Scale = new ContainerAppScale { MinReplicas = spec.MinReplicas, MaxReplicas = spec.MaxReplicas },
        };
        template.Containers.Add(container);
        foreach (var mount in spec.VolumeMounts)
        {
            template.Volumes.Add(new ContainerAppVolume { Name = mount.Volume, StorageType = ContainerAppStorageType.AzureFile, StorageName = mount.EnvironmentStorage, MountOptions = mount.MountOptions });
        }

        foreach (var file in spec.SecretFiles)
        {
            var volume = new ContainerAppVolume { Name = SecretVolume(file), StorageType = ContainerAppStorageType.Secret };
            volume.Secrets.Add(new SecretVolumeItem { SecretRef = file.Name, Path = FileName(file) });
            template.Volumes.Add(volume);
        }

        var existing = await rg.GetContainerApps().GetIfExistsAsync(spec.Name, ct);
        var configuration = new ContainerAppConfiguration
        {
            // The old revision is deactivated only once the new one is ready; lease ownership covers the overlap.
            ActiveRevisionsMode = ContainerAppActiveRevisionsMode.Single,
            Ingress = new ContainerAppIngressConfiguration { External = spec.Ingress.External, TargetPort = targetPort, Transport = ContainerAppIngressTransportMethod.Auto },
        };
        // Domains and their certificate bindings are managed outside ResourceSpec. A template update
        // must retain these ingress bindings rather than replacing them with an empty collection.
        if (existing.HasValue && existing.Value!.Data.Configuration?.Ingress is { } currentIngress)
        {
            foreach (var domain in currentIngress.CustomDomains) { configuration.Ingress.CustomDomains.Add(domain); }
        }
        foreach (var port in spec.InternalPorts)
        {
            configuration.Ingress.AdditionalPortMappings.Add(new IngressPortMapping(false, port) { ExposedPort = port });
        }
        foreach (var secret in spec.Secrets)
        {
            configuration.Secrets.Add(new ContainerAppWritableSecret
            {
                Name = secret.Name,
                KeyVaultUri = new Uri($"https://{secret.Vault}.vault.azure.net/secrets/{secret.VaultSecret}"),
                Identity = identityId,
            });
        }
        foreach (var file in spec.SecretFiles)
        {
            configuration.Secrets.Add(new ContainerAppWritableSecret
            {
                Name = file.Name,
                KeyVaultUri = new Uri($"https://{file.Vault}.vault.azure.net/secrets/{file.VaultSecret}"),
                Identity = identityId,
            });
        }
        if (spec.RegistryServer.Length > 0)
        {
            configuration.Registries.Add(new ContainerAppRegistryCredentials { Server = spec.RegistryServer, Identity = identityId });
        }
        foreach (var registry in spec.Registries.Where(entry => entry.Server != spec.RegistryServer))
        {
            if (registry.PasswordSecret.Length > 0)
            {
                var secretName = "registry-" + new string([.. registry.Server.Where(char.IsAsciiLetterOrDigit)]);
                configuration.Secrets.Add(new ContainerAppWritableSecret
                {
                    Name = secretName,
                    KeyVaultUri = new Uri($"https://{registry.Vault}.vault.azure.net/secrets/{registry.PasswordSecret}"),
                    Identity = identityId,
                });
                configuration.Registries.Add(new ContainerAppRegistryCredentials { Server = registry.Server, Username = registry.Username, PasswordSecretRef = secretName });
            }
            else
            {
                var pullIdentity = registry.Identity.Length > 0 ? (await rg.GetUserAssignedIdentityAsync(registry.Identity, ct)).Value.Id.ToString() : identityId;
                configuration.Registries.Add(new ContainerAppRegistryCredentials { Server = registry.Server, Identity = pullIdentity });
            }
        }

        var data = new ContainerAppData(location)
        {
            EnvironmentId = environment.Id,
            WorkloadProfileName = "Consumption",
            Identity = new ManagedServiceIdentity(ManagedServiceIdentityType.UserAssigned),
            Configuration = configuration,
            Template = template,
        };
        data.Identity.UserAssignedIdentities[identity.Id] = new UserAssignedIdentity();
        Apply(spec, data.Tags);
        // ARM adds read-only/default fields. Requested arrays must match exactly, including removals.
        using var desiredJson = JsonDocument.Parse(ModelReaderWriter.Write(data).ToString());
        using var existingJson = existing.HasValue ? JsonDocument.Parse(ModelReaderWriter.Write(existing.Value!.Data).ToString()) : null;
        if (existingJson is not null && MatchesDesired(desiredJson.RootElement, existingJson.RootElement))
        {
            return Ref(existing.Value!.Id, spec, spec.Name);
        }
        var made = await rg.GetContainerApps().CreateOrUpdateAsync(WaitUntil.Completed, spec.Name, data, ct);
        return Ref(made.Value.Id, spec, spec.Name);
    }

    private static bool MatchesDesired(JsonElement desired, JsonElement current)
    {
        if (desired.ValueKind != current.ValueKind) { return false; }
        return desired.ValueKind switch
        {
            JsonValueKind.Object => desired.EnumerateObject().All(property => current.TryGetProperty(property.Name, out var value) && MatchesDesired(property.Value, value))
                && current.EnumerateObject().All(property => !IsManagedCollection(property.Name) || desired.TryGetProperty(property.Name, out _) || property.Value.ValueKind != JsonValueKind.Array || property.Value.GetArrayLength() == 0),
            JsonValueKind.Array => desired.GetArrayLength() == current.GetArrayLength() && desired.EnumerateArray().Zip(current.EnumerateArray()).All(pair => MatchesDesired(pair.First, pair.Second)),
            _ => JsonElement.DeepEquals(desired, current),
        };
    }

    // The SDK omits empty collections when serializing a desired resource; old entries still need removing.
    private static bool IsManagedCollection(string name) => name is "containers" or "env" or "probes" or "volumeMounts" or "volumes" or "secrets" or "registries" or "additionalPortMappings";

    private static string SecretVolume(SecretFileSpec file) => "secret-" + file.Name;

    private static string FileName(SecretFileSpec file) => file.Path[(file.Path.LastIndexOf('/') + 1)..];

    private static async Task<ResourceRef> EnsureNetworkSecurityGroup(ResourceGroupResource rg, NetworkSecurityGroupSpec spec, AzureLocation location, CancellationToken ct)
    {
        var data = new NetworkSecurityGroupData { Location = location };
        foreach (var rule in spec.Rules)
        {
            data.SecurityRules.Add(new SecurityRuleData
            {
                Name = rule.Name,
                Priority = rule.Priority,
                Direction = new SecurityRuleDirection(rule.Direction),
                Access = new SecurityRuleAccess(rule.Access),
                Protocol = new SecurityRuleProtocol(rule.Protocol),
                SourceAddressPrefix = "*",
                SourcePortRange = "*",
                DestinationAddressPrefix = rule.DestinationAddressPrefix,
                DestinationPortRange = rule.DestinationPortRange,
            });
        }
        Apply(spec, data.Tags);
        var made = await rg.GetNetworkSecurityGroups().CreateOrUpdateAsync(WaitUntil.Completed, spec.Name, data, ct);
        return Ref(made.Value.Id, spec, spec.Name);
    }

    private static async Task<ResourceRef> EnsureVirtualNetwork(ResourceGroupResource rg, VirtualNetworkSpec spec, AzureLocation location, CancellationToken ct)
    {
        var data = new VirtualNetworkData { Location = location };
        data.AddressPrefixes.Add(spec.AddressSpace);
        Apply(spec, data.Tags);
        var made = await rg.GetVirtualNetworks().CreateOrUpdateAsync(WaitUntil.Completed, spec.Name, data, ct);
        return Ref(made.Value.Id, spec, spec.Name);
    }

    private static async Task<ResourceRef> EnsureSubnet(ResourceGroupResource rg, SubnetSpec spec, CancellationToken ct)
    {
        var network = (await rg.GetVirtualNetworkAsync(spec.VirtualNetwork, cancellationToken: ct)).Value;
        var data = new SubnetData { Name = spec.Name, AddressPrefix = spec.AddressPrefix };
        if (spec.NetworkSecurityGroup.Length > 0)
        {
            var group = (await rg.GetNetworkSecurityGroupAsync(spec.NetworkSecurityGroup, cancellationToken: ct)).Value;
            data.NetworkSecurityGroup = new NetworkSecurityGroupData { Id = group.Id };
        }
        if (spec.DelegationService.Length > 0)
        {
            data.Delegations.Add(new ServiceDelegation { Name = spec.Name, ServiceName = spec.DelegationService });
        }
        var made = await network.GetSubnets().CreateOrUpdateAsync(WaitUntil.Completed, spec.Name, data, ct);
        return Ref(made.Value.Id, spec, spec.Name);
    }

    private static async Task<ResourceRef> EnsureSessionPool(ResourceGroupResource rg, SessionPoolSpec spec, AzureLocation location, CancellationToken ct)
    {
        var environment = (await rg.GetContainerAppManagedEnvironmentAsync(spec.Environment, ct)).Value;
        var identity = (await rg.GetUserAssignedIdentityAsync(spec.Identity, ct)).Value;
        var data = new SessionPoolData(location)
        {
            EnvironmentId = environment.Id,
            ContainerType = ContainerType.CustomContainer,
            PoolManagementType = PoolManagementType.Dynamic,
            Identity = new ManagedServiceIdentity(ManagedServiceIdentityType.UserAssigned),
            ScaleConfiguration = new SessionPoolScaleConfiguration { MaxConcurrentSessions = spec.MaxSessions, ReadySessionInstances = spec.ReadySessions },
            // A session lives while its caller needs it; the daily recycle is the pool's cap.
            DynamicPoolLifecycleConfiguration = new SessionPoolLifecycleConfiguration
            {
                LifecycleType = SessionPoolLifecycleType.OnContainerExit,
                MaxAlivePeriodInSeconds = spec.MaxAliveSeconds,
            },
            SessionNetworkStatus = spec.Egress ? SessionNetworkStatus.EgressEnabled : SessionNetworkStatus.EgressDisabled,
        };
        data.Identity.UserAssignedIdentities[identity.Id] = new UserAssignedIdentity();
        data.ManagedIdentitySettings.Add(new SessionPoolManagedIdentitySetting(identity.Id.ToString())
        {
            Lifecycle = ContainerAppIdentitySettingsLifeCycle.None,
        });
        var template = new CustomContainerTemplate
        {
            IngressTargetPort = spec.TargetPort,
            RegistryCredentials = new SessionRegistryCredentials { Server = spec.RegistryServer, Identity = identity.Id.ToString() },
        };
        template.Containers.Add(new SessionContainer
        {
            Name = "session",
            Image = spec.Image,
            Resources = new SessionContainerResources { Cpu = spec.Cpu, Memory = spec.Memory },
        });
        data.CustomContainerTemplate = template;
        Apply(spec, data.Tags);
        var made = await rg.GetSessionPools().CreateOrUpdateAsync(WaitUntil.Completed, spec.Name, data, ct);
        return Ref(made.Value.Id, spec, spec.Name) with { Endpoint = made.Value.Data.PoolManagementEndpoint?.AbsoluteUri ?? "" };
    }

    private async Task<ResourceRef> EnsureGrant(ResourceGroupResource rg, RoleGrantSpec spec, CancellationToken ct)
    {
        var principal = Guid.TryParse(spec.Principal, out var id)
            ? id
            : (await rg.GetUserAssignedIdentityAsync(spec.Principal, ct)).Value.Data.PrincipalId
                ?? throw new InvalidOperationException($"Identity '{spec.Principal}' has no principal id.");
        var subscription = rg.Id.SubscriptionId;
        var definition = $"/subscriptions/{subscription}/providers/Microsoft.Authorization/roleDefinitions/{spec.RoleDefinitionId}";
        var content = new RoleAssignmentCreateOrUpdateContent(new ResourceIdentifier(definition), principal)
        {
            PrincipalType = RoleManagementPrincipalType.ServicePrincipal,
        };
        var assignments = Client.GetRoleAssignments(new ResourceIdentifier(spec.Scope));
        var made = await assignments.CreateOrUpdateAsync(WaitUntil.Completed, GrantName(spec.Scope, principal, spec.RoleDefinitionId), content, ct);
        return Ref(made.Value.Id, spec, spec.Name);
    }

    // A role assignment's name is a guid; deriving it makes a second PUT hit the same assignment.
    internal static string GrantName(string scope, Guid principal, string role)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{scope}|{principal}|{role}".ToLowerInvariant()));
        return new Guid(hash.AsSpan(0, 16)).ToString();
    }
}
