using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Modules.Microsoft.Azure.Install;
using IntoChat.OS.ServiceDefaults;

namespace IntoChat.OS.Modules.Microsoft.Azure.Tests.Unit;

// A projection fact: the variables an install writes for the server are its subject (P2-D-22, 03-12). The cloud topology reads one
// account and one vault; the retired orleans integration fields and the old vault key are never written, so a fresh install
// cannot trip the startup guard.
public sealed class InstallPlanTopologyFacts
{
    private static readonly InstallConfig Sample = new()
    {
        Subscription = "00000000-0000-0000-0000-000000000001",
        Location = "westeurope",
        Prefix = "intochat",
        Env = "test",
        OsImage = "ghcr.io/intochat/os:1.0.0",
    };

    private static ContainerAppSpec App() => InstallPlan.Build(Sample).OfType<ContainerAppSpec>().Single();

    [Fact]
    public void The_app_selects_the_cloud_topology_over_the_install_account_and_vault()
    {
        var specs = InstallPlan.Build(Sample);
        var account = specs.OfType<StorageAccountSpec>().Single().Name;
        var vault = specs.OfType<KeyVaultSpec>().Single().Name;
        var variables = App().Variables;
        Assert.Equal(IntoChatOSNames.TopologyCloud, variables[ConfigurationKeyGrammar.EnvironmentName(IntoChatOSNames.TopologyKey)]);
        Assert.Equal($"https://{account}.blob.core.windows.net/", variables[ConfigurationKeyGrammar.EnvironmentName(IntoChatOSNames.CloudStorageConfigurationKey)]);
        Assert.Equal($"https://{vault}.vault.azure.net/", variables[ConfigurationKeyGrammar.EnvironmentName(IntoChatOSNames.CloudKeyVaultConfigurationKey)]);
    }

    [Fact]
    public void The_app_keeps_its_grain_state_container_as_the_orleans_module_option()
    {
        var key = ConfigurationKeyGrammar.EnvironmentName(
            IntoChat.OS.Framework.Modules.Configuration.IntoChatOSConfiguration.ModuleOptionsSection(OrleansIntegrationNames.Id) + ":" + OrleansIntegrationNames.GrainStateContainer);
        Assert.Equal("grainstate-v1", App().Variables[key]);
    }

    [Fact]
    public void The_app_writes_none_of_the_retired_variables()
    {
        var variables = App().Variables;
        var retired = LegacyConfigurationGuard.RetiredSettings.Select(row => ConfigurationKeyGrammar.EnvironmentName(row.Name)).ToArray();
        Assert.DoesNotContain(variables.Keys, key => retired.Contains(key, StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain(variables.Keys, key => key.StartsWith(ConfigurationKeyGrammar.EnvironmentName(IntoChatOSNames.IntegrationsSectionName), StringComparison.OrdinalIgnoreCase));
    }
}
