using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Modules.Microsoft.Azure;
using IntoChat.OS.Modules.Microsoft.Azure.Install;

namespace IntoChat.OS.Modules.Microsoft.Azure.Tests.Unit;

public sealed class AzureFacts
{
    private static readonly InstallConfig Sample = new()
    {
        Subscription = "00000000-0000-0000-0000-000000000001",
        Location = "westeurope",
        Prefix = "intochat",
        Env = "test",
        OsImage = "ghcr.io/intochat/os:1.0.0",
    };

    public static TheoryData<ResourceSpec> OneSpecPerKind() =>
        new(InstallPlan.Build(Sample).GroupBy(spec => spec.Kind).Select(group => group.First()));

    [Fact]
    public void The_plan_has_one_spec_for_every_kind_of_this_change()
    {
        var kinds = InstallPlan.Build(Sample).Select(spec => spec.Kind).Distinct().Order().ToArray();
        string[] expected =
        [
            ResourceKinds.BlobContainer, ResourceKinds.ContainerApp, ResourceKinds.FileShare, ResourceKinds.Identity, ResourceKinds.KeyVault,
            ResourceKinds.LogWorkspace, ResourceKinds.ManagedEnvironment, ResourceKinds.Registry, ResourceKinds.ResourceGroup, ResourceKinds.RoleGrant,
            ResourceKinds.Secret, ResourceKinds.StorageAccount,
        ];
        Assert.Equal(expected.Order(), kinds);
    }

    [Theory]
    [MemberData(nameof(OneSpecPerKind))]
    public async Task Ensure_returns_the_deterministic_name_and_merged_tags(ResourceSpec spec)
    {
        var arm = new RecordingAzure();
        var neuron = Neuron(arm);
        var withTag = spec with { Tags = new Dictionary<string, string> { ["team"] = "core", ["owner"] = "custom" } };

        var made = await neuron.Ensure(withTag);

        Assert.Equal(spec.Name, made.Name);
        Assert.Equal(spec.Kind, made.Kind);
        var recorded = Assert.Single(arm.Ensured);
        Assert.Equal("intochat", recorded.Tags["managed-by"]);
        Assert.Equal("test", recorded.Tags["env"]);
        Assert.Equal("custom", recorded.Tags["owner"]);
        Assert.Equal("core", recorded.Tags["team"]);
    }

    [Fact]
    public void Names_follow_the_prefix_env_kind_order_within_azure_limits()
    {
        Assert.Equal("intochat-test-rg", ResourceNames.ResourceGroup("intochat", "test"));
        Assert.Equal("intochattestst", ResourceNames.StorageAccount("intochat", "test"));
        Assert.True(ResourceNames.KeyVault("intochat-long-prefix", "production").Length <= 24);
    }

    [Fact]
    public async Task The_dry_run_lists_the_resources_in_dependency_order()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var file = Path.Combine(directory.FullName, "test.json");
            await File.WriteAllTextAsync(file, """
                {"subscription":"00000000-0000-0000-0000-000000000001","location":"westeurope","prefix":"intochat","env":"test","osImage":"ghcr.io/intochat/os:1.0.0"}
                """, TestContext.Current.CancellationToken);
            var arm = new RecordingAzure();
            var output = new StringWriter();

            var code = await AzureInstaller.Run(["install", "--env", "test", "--config", file, "--dry-run"], arm, [], output, new StringWriter(), TestContext.Current.CancellationToken);

            Assert.Equal(0, code);
            Assert.Empty(arm.Ensured);
            string[] expected =
            [
                "ensure resource-group intochat-test-rg in intochat-test-rg",
                "ensure identity intochat-test-id in intochat-test-rg",
                "ensure log-workspace intochat-test-log in intochat-test-rg",
                "ensure storage-account intochattestst in intochat-test-rg",
                "ensure file-share brainlog in intochat-test-rg",
                "ensure blob-container grainstate-v1 in intochat-test-rg",
                "ensure blob-container brainlease-v1 in intochat-test-rg",
                "ensure registry intochattestacr in intochat-test-rg",
                "ensure key-vault intochat-test-kv in intochat-test-rg",
                "ensure managed-environment intochat-test-env in intochat-test-rg",
                "ensure role-grant intochat-test-os-vault-secrets in intochat-test-rg",
                "ensure role-grant intochat-test-os-storage-blobs in intochat-test-rg",
                "ensure role-grant intochat-test-os-storage-tables in intochat-test-rg",
                "ensure role-grant intochat-test-os-registry-pull in intochat-test-rg",
                "ensure secret master-key in intochat-test-rg",
                "ensure container-app intochat-test-os in intochat-test-rg",
                "summary: 16 resources in intochat-test-rg (blob-container=2, container-app=1, file-share=1, identity=1, key-vault=1, log-workspace=1, managed-environment=1, registry=1, resource-group=1, role-grant=4, secret=1, storage-account=1)",
            ];
            Assert.Equal(expected, output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public void The_app_carries_the_runtime_wiring_the_deployment_set_before()
    {
        var app = InstallPlan.Build(Sample).OfType<ContainerAppSpec>().Single();
        Assert.Equal(ContainerAppSpec.IdentityClientIdToken, app.Variables["AZURE_CLIENT_ID"]);
        Assert.Equal("intochat-test", app.Variables.Single(pair => pair.Key.EndsWith("ClusterId", StringComparison.Ordinal)).Value);
        Assert.Equal("grainstate-v1", app.Variables.Single(pair => pair.Key.EndsWith("GrainStateContainer", StringComparison.Ordinal)).Value);
        Assert.Equal(IntoChatOSNames.MasterKeyEnvironmentVariable, Assert.Single(app.Secrets).Variable);
        Assert.Equal("/data/brain/v1", Assert.Single(app.VolumeMounts).MountPath);
    }

    // Azure Container Apps refuses unsupported SMB mount options (ContainerAppVolumeMountOptionsNotSupported for actimeo).
    [Fact]
    public void The_brain_log_share_uses_only_mount_options_container_apps_accepts()
    {
        var options = Assert.Single(InstallPlan.Build(Sample).OfType<ContainerAppSpec>().Single().VolumeMounts).MountOptions!.Split(',');
        Assert.DoesNotContain(options, option => option.StartsWith("actimeo", StringComparison.Ordinal));
        Assert.Contains("cache=strict", options);
    }

    [Fact]
    public async Task A_failing_install_stops_at_the_first_error_and_exits_non_zero()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var file = Path.Combine(directory.FullName, "test.json");
            await File.WriteAllTextAsync(file, """
                {"subscription":"s","location":"westeurope","prefix":"intochat","env":"test","osImage":"img"}
                """, TestContext.Current.CancellationToken);
            var error = new StringWriter();
            var secrets = Path.Combine(directory.FullName, "secrets.json");
            await File.WriteAllTextAsync(secrets, "{\"master-key\":\"k\"}", TestContext.Current.CancellationToken);
            var arm = new FailingAfter(2);

            var code = await AzureInstaller.Run(["install", "--env", "test", "--config", file, "--secrets-from", secrets], arm, [], new StringWriter(), error, TestContext.Current.CancellationToken);

            Assert.Equal(1, code);
            Assert.Equal(2, arm.Ensured.Count);
            Assert.Contains("boom", error.ToString(), StringComparison.Ordinal);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task Inventory_returns_what_was_ensured()
    {
        var neuron = Neuron(new RecordingAzure());
        var plan = InstallPlan.Build(Sample).Take(3).ToArray();
        foreach (var spec in plan) { await neuron.Ensure(spec); }

        var inventory = await neuron.Inventory("test");

        Assert.Equal(plan.Select(spec => spec.Name), inventory.Select(resource => resource.Name));
    }

    [Fact]
    public async Task A_second_ensure_with_the_same_spec_records_one_ref()
    {
        var neuron = Neuron(new RecordingAzure());
        var spec = InstallPlan.Build(Sample)[0];

        var first = await neuron.Ensure(spec);
        var second = await neuron.Ensure(spec);

        Assert.Equal(first, second);
        Assert.Single(await neuron.Inventory("test"));
    }

    private static AzureNeuron Neuron(IAzureArm arm) => AzureNeurons.Create(arm, Sample.Subscription);

    private sealed class FailingAfter(int succeed) : IAzureArm
    {
        private readonly RecordingAzure _inner = new();

        public List<ResourceSpec> Ensured => _inner.Ensured;

        public Task<ResourceRef> Ensure(string subscription, ResourceSpec spec, CancellationToken ct) =>
            _inner.Ensured.Count >= succeed ? throw new InvalidOperationException("boom") : _inner.Ensure(subscription, spec, ct);

        public Task<string?> ReadSecret(string vault, string name, CancellationToken ct) => _inner.ReadSecret(vault, name, ct);

        public Task<ResourceRef?> Read(string subscription, ResourceRef resource, CancellationToken ct) => _inner.Read(subscription, resource, ct);

        public Task Remove(string subscription, ResourceRef resource, CancellationToken ct) => _inner.Remove(subscription, resource, ct);

        public Task<ResourceRef[]> List(string subscription, string tagValue, CancellationToken ct) => _inner.List(subscription, tagValue, ct);
    }
}
