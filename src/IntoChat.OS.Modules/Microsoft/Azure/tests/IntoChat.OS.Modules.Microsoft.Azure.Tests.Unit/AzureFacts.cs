using IntoChat.OS.Modules.Microsoft.Azure;

namespace IntoChat.OS.Modules.Microsoft.Azure.Tests.Unit;

public sealed class AzureFacts
{
    private const string Subscription = "00000000-0000-0000-0000-000000000001";

    private static readonly ResourceSpec[] SampleSpecs =
    [
        new ResourceGroupSpec { Name = "intochat-test-rg", ResourceGroup = "intochat-test-rg", Location = "westeurope", Env = "test" },
        new IdentitySpec { Name = "intochat-test-id", ResourceGroup = "intochat-test-rg", Location = "westeurope", Env = "test" },
        new LogWorkspaceSpec { Name = "intochat-test-log", ResourceGroup = "intochat-test-rg", Location = "westeurope", Env = "test" },
        new StorageAccountSpec { Name = "intochattestst", ResourceGroup = "intochat-test-rg", Location = "westeurope", Env = "test" },
        new FileShareSpec { Name = "brainlog", ResourceGroup = "intochat-test-rg", Location = "westeurope", Env = "test", StorageAccount = "intochattestst" },
        new BlobContainerSpec { Name = "grainstate-v1", ResourceGroup = "intochat-test-rg", Location = "westeurope", Env = "test", StorageAccount = "intochattestst" },
        new RegistrySpec { Name = "intochattestacr", ResourceGroup = "intochat-test-rg", Location = "westeurope", Env = "test" },
        new KeyVaultSpec { Name = "intochat-test-kv", ResourceGroup = "intochat-test-rg", Location = "westeurope", Env = "test" },
        new ManagedEnvironmentSpec { Name = "intochat-test-env", ResourceGroup = "intochat-test-rg", Location = "westeurope", Env = "test", LogWorkspace = "intochat-test-log" },
        new RoleGrantSpec { Name = "intochat-test-os-vault-secrets", ResourceGroup = "intochat-test-rg", Location = "westeurope", Env = "test", Principal = "intochat-test-id", RoleDefinitionId = "role", Scope = "scope" },
        new SecretSpec { Name = "master-key", ResourceGroup = "intochat-test-rg", Location = "westeurope", Env = "test", Vault = "intochat-test-kv", Value = "v" },
        new ContainerAppSpec { Name = "intochat-test-os", ResourceGroup = "intochat-test-rg", Location = "westeurope", Env = "test", Environment = "intochat-test-env", Image = "ghcr.io/intochat/os:1.0.0" },
    ];

    public static TheoryData<ResourceSpec> OneSpecPerKind() => new(SampleSpecs);

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
    public async Task Inventory_returns_what_was_ensured()
    {
        var neuron = Neuron(new RecordingAzure());
        var plan = SampleSpecs.Take(3).ToArray();
        foreach (var spec in plan) { await neuron.Ensure(spec); }

        var inventory = await neuron.Inventory("test");

        Assert.Equal(plan.Select(spec => spec.Name), inventory.Select(resource => resource.Name));
    }

    [Fact]
    public async Task A_second_ensure_with_the_same_spec_records_one_ref()
    {
        var neuron = Neuron(new RecordingAzure());
        var spec = SampleSpecs[0];

        var first = await neuron.Ensure(spec);
        var second = await neuron.Ensure(spec);

        Assert.Equal(first, second);
        Assert.Single(await neuron.Inventory("test"));
    }

    private static AzureNeuron Neuron(IAzureArm arm) => AzureNeurons.Create(arm, Subscription);
}
