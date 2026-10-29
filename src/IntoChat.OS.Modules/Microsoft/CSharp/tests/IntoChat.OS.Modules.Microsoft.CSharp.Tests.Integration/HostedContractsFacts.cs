using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

// A module that hosts its own contracts (Time here) is carried by what it declares, for its contract surface only.
public sealed class HostedContractsFacts
{
    [Fact]
    public void AModuleThatHostsItsContractsIsInTheScriptReferencesWithoutItsImplementationRing()
    {
        var references = CSharpScriptCheck.ReferenceNames();
        Assert.Contains("IntoChat.OS.Modules.Time", references);
        Assert.DoesNotContain("IntoChat.OS.Framework", references);
        Assert.DoesNotContain("Microsoft.Orleans.Reminders", references);
        Assert.True(IntoChat.OS.Client.Runtime.ContractsHost.Declares(typeof(IntoChat.OS.Modules.Time.Timers.ITimer).Assembly));
        Assert.Contains(typeof(IntoChat.OS.Modules.Time.Timers.ITimer), IntoChat.OS.Client.Runtime.ContractsHost.ContractTypes(typeof(IntoChat.OS.Modules.Time.Timers.ITimer).Assembly));
    }
}
