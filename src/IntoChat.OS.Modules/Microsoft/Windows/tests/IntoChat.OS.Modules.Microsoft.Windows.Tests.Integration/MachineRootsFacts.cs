using DigitalBrain.Capabilities.Storage;
using IntoChat.OS.Runtime.Storage.Confinement;

namespace IntoChat.OS.Modules.Microsoft.Windows.Tests.Integration;

// The corpus lives in HostilePathFacts. This fact checks the Windows storage path still refuses a hostile id.
public sealed class MachineRootsFacts
{
    [Fact]
    public async Task AHostileListIsDenied()
    {
        var root = Directory.CreateTempSubdirectory("windows-confine").FullName;
        try
        {
            var storage = new WindowsStorage([PathConfinement.Normalize(root)]);
            var outside = Path.Combine(Directory.GetParent(root)!.FullName, "escape.txt");
            var error = await Assert.ThrowsAsync<Denied>(() => storage.List(WindowsStorage.IdOf(outside), new ListQuery(null, 10), TestContext.Current.CancellationToken));
            Assert.IsType<ConfinementDenied>(error.InnerException);
            Assert.Equal(ConfinementDenied.FixedMessage, error.InnerException.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
