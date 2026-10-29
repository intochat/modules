using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Modules.Microsoft.Azure.Install;

namespace IntoChat.OS.Modules.Microsoft.Azure.Tests.Unit;

public sealed class ModuleSecretFacts
{
    [Fact]
    public async Task Installer_provides_module_secret_values_and_keeps_them_out_of_output()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var config = Path.Combine(directory.FullName, "test.json");
            var secrets = Path.Combine(directory.FullName, "secrets.json");
            await File.WriteAllTextAsync(config, """
                {"subscription":"00000000-0000-0000-0000-000000000001","location":"polandcentral","prefix":"intochat","env":"test","osImage":"os:1",
                 "modules":{"orphan":{"secret":"GoogleAuth:ClientSecret"}}}
                """, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(secrets, """{"master-key":"test-master","GoogleAuth:ClientSecret":"private-test-value"}""", TestContext.Current.CancellationToken);
            var azure = new RecordingAzure();
            var output = new StringWriter();
            var error = new StringWriter();

            var code = await AzureInstaller.Run(["install", "--env", "test", "--config", config, "--secrets-from", secrets], azure,
                [OsModule.FromDigital(typeof(ModuleDeploymentFacts.OrphanModule))], output, error, TestContext.Current.CancellationToken);

            Assert.Equal(0, code);
            Assert.Equal("private-test-value", azure.Vault["module-secret"]);
            Assert.DoesNotContain("private-test-value", output.ToString() + error, StringComparison.Ordinal);

            await File.WriteAllTextAsync(secrets, """{"master-key":"test-master","Other":"private-test-value"}""", TestContext.Current.CancellationToken);
            azure = new RecordingAzure();
            error = new StringWriter();
            code = await AzureInstaller.Run(["install", "--env", "test", "--config", config, "--secrets-from", secrets], azure,
                [OsModule.FromDigital(typeof(ModuleDeploymentFacts.OrphanModule))], output, error, TestContext.Current.CancellationToken);
            Assert.Equal(1, code);
            Assert.Contains("GoogleAuth:ClientSecret", error.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("private-test-value", error.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(azure.Ensured, spec => spec.Name == "module-secret");

            await File.WriteAllTextAsync(secrets, """{"master-key":"test-master","GoogleAuth:ClientSecret":null}""", TestContext.Current.CancellationToken);
            error = new StringWriter();
            code = await AzureInstaller.Run(["install", "--env", "test", "--config", config, "--secrets-from", secrets], new RecordingAzure(),
                [OsModule.FromDigital(typeof(ModuleDeploymentFacts.OrphanModule))], output, error, TestContext.Current.CancellationToken);
            Assert.Equal(1, code);
            Assert.Contains("GoogleAuth:ClientSecret", error.ToString(), StringComparison.Ordinal);

            azure = new RecordingAzure();
            Assert.Equal(0, await AzureInstaller.Run(["install", "--env", "test", "--config", config, "--dry-run"], azure,
                [OsModule.FromDigital(typeof(ModuleDeploymentFacts.OrphanModule))], output, error, TestContext.Current.CancellationToken));
            Assert.Empty(azure.Ensured);
        }
        finally { directory.Delete(true); }
    }
}
