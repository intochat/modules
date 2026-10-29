using IntoChat.OS.Runtime.Storage.Confinement;
using MicrosoftSdk = Microsoft;

namespace IntoChat.OS.Modules.Microsoft.Windows.Tests.Unit;

public sealed class MachineFacts : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "intochat-windows-unit-" + Guid.NewGuid().ToString("N"));

    public MachineFacts() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ARelativePathResolvesAgainstTheFirstRoot()
    {
        var first = Path.Combine(_root, "one");
        var second = Path.Combine(_root, "two");

        Assert.Equal(Path.Combine(first, "sub", "file.txt"), PathConfinement.Resolve([first, second], Path.Combine("sub", "file.txt")));
    }

    [Fact]
    public void AnAbsolutePathInsideAnyRootIsAccepted()
    {
        var first = Path.Combine(_root, "one");
        var second = Path.Combine(_root, "two");
        var inside = Path.Combine(second, "a", "b.txt");

        Assert.Equal(inside, PathConfinement.Resolve([first, second], inside));
        Assert.Equal(second, PathConfinement.Resolve([first, second], second + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void ASiblingFolderSharingTheRootsPrefixIsOutsideIt()
    {
        var root = Path.Combine(_root, "root");

        Assert.Throws<ConfinementDenied>(() => PathConfinement.Resolve([root], root + "2"));
        Assert.Throws<ConfinementDenied>(() => PathConfinement.Resolve([root], Path.Combine("..", "elsewhere")));
    }

    [Fact]
    public void NoRootsMeansNothingIsAllowedAndABlankPathIsRefused()
    {
        Assert.Throws<ConfinementDenied>(() => PathConfinement.Resolve([], "x"));
        Assert.Throws<ConfinementDenied>(() => PathConfinement.Resolve([_root], " "));
    }

    [Fact]
    public void NormalizeDropsATrailingSeparatorButKeepsADriveRoot()
    {
        Assert.Equal(Path.Combine(_root, "dir"), PathConfinement.Normalize(Path.Combine(_root, "dir") + Path.DirectorySeparatorChar));
        var filesystemRoot = Path.GetPathRoot(_root)!;
        Assert.Equal(filesystemRoot, PathConfinement.Normalize(filesystemRoot));
    }

    [Fact]
    public void TheMachineIdIsCreatedOnceThenReadBack()
    {
        var file = Path.Combine(_root, "state", "machine-id");
        var options = MicrosoftSdk.Extensions.Options.Options.Create(new WindowsOptions { MachineIdFile = file });

        var created = new MachineIdentity(options).Id;

        Assert.True(Guid.TryParseExact(created, "N", out _));
        Assert.Equal(created, new MachineIdentity(options).Id);
        Assert.Equal(created, File.ReadAllText(file));
    }

    [Fact]
    public void AGarbledMachineIdFileIsAnErrorAndIsNeverReplaced()
    {
        var file = Path.Combine(_root, "machine-id");
        File.WriteAllText(file, "not-a-guid");
        var options = MicrosoftSdk.Extensions.Options.Options.Create(new WindowsOptions { MachineIdFile = file });

        Assert.Throws<InvalidDataException>(() => new MachineIdentity(options).Id);
        Assert.Equal("not-a-guid", File.ReadAllText(file));
    }

    [Fact]
    public void TheDefaultOptionsLeaveTheMachineIdFileToTheLocalAppDataDefault()
    {
        var options = new WindowsOptions();

        options.Validate();
        Assert.Equal("", options.MachineIdFile);
    }
}
