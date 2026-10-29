using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Microsoft.Windows.Tests.Integration;

public sealed class MachineIdentityFacts
{
    [Fact]
    public void TheFirstRunCreatesAnIdAndLaterRunsReadTheSameOne()
    {
        var file = Path.Combine(Directory.CreateTempSubdirectory("machine-id").FullName, "machine-id");
        var options = Options.Create(new WindowsOptions { MachineIdFile = file });

        var first = new MachineIdentity(options).Id;
        var second = new MachineIdentity(options).Id;

        Assert.Equal(first, second);
        Assert.True(Guid.TryParseExact(first, "N", out _));
        Assert.Equal(first, File.ReadAllText(file).Trim());
    }

    [Fact]
    public void AGarbledIdFileIsRefusedRatherThanReplaced()
    {
        var file = Path.Combine(Directory.CreateTempSubdirectory("machine-id").FullName, "machine-id");
        File.WriteAllText(file, "not-a-guid");

        Assert.Throws<InvalidDataException>(() => new MachineIdentity(Options.Create(new WindowsOptions { MachineIdFile = file })).Id);
    }
}
