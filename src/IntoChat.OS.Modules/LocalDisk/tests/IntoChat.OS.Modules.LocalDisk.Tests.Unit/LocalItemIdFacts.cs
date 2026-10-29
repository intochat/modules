using DigitalBrain.Capabilities.Storage;
using IntoChat.OS.Runtime.Storage.Confinement;

namespace IntoChat.OS.Modules.LocalDisk.Tests.Unit;

public sealed class LocalItemIdFacts
{
    [Fact]
    public void RoundTripKeepsRootAndRelative()
    {
        var id = LocalItemId.Of("root1", "Notes/a.txt");
        var (root, relative) = LocalItemId.Read(id);
        Assert.Equal("root1", root);
        Assert.Equal("Notes/a.txt", relative);
        Assert.StartsWith("ld:", id.Value);
        Assert.DoesNotContain("Notes", id.Value);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/../../b")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("folder//file")]
    public void HostileRelativeIsDenied(string relative)
    {
        var id = LocalItemId.Of("root1", relative);
        var error = Assert.Throws<Denied>(() => LocalItemId.Read(id));
        Assert.Equal("denied", error.Code);
    }

    [Fact]
    public void ForeignPrefixIsNotFound()
    {
        var error = Assert.Throws<NotFound>(() => LocalItemId.Read(new ItemId("nope")));
        Assert.Equal("not-found", error.Code);
    }
}

public sealed class LocalDiskRootFacts
{
    [Fact]
    public void SyntaxRejectsBlankAndRelativePaths()
    {
        Assert.Equal(LocalDiskRoots.NeedFullPath, LocalDiskRoots.Syntax(null));
        Assert.Equal(LocalDiskRoots.NeedFullPath, LocalDiskRoots.Syntax("  "));
        Assert.Equal(LocalDiskRoots.NeedFullPath, LocalDiskRoots.Syntax("notes"));
        Assert.Null(LocalDiskRoots.Syntax(Path.GetTempPath()));
    }

    [Fact]
    public void PrepareAcceptsARealFolderAndRefusesWindows()
    {
        var folder = PathConfinement.Normalize(Directory.CreateTempSubdirectory("localdisk-root").FullName);
        Assert.Equal(folder, LocalDiskRoots.Prepare(folder));
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (windows.Length > 0)
        {
            var error = Assert.Throws<ConfinementDenied>(() => LocalDiskRoots.Prepare(windows));
            Assert.Equal(LocalDiskRoots.Unavailable, LocalDiskRoots.FailureText(error));
            Assert.NotEqual(LocalDiskRoots.Unavailable, error.Message);
        }
    }
}

public sealed class LocalDiskModuleOnTestOS : DigitalBrain.Testing.ModuleOnTestOSFacts<LocalDiskModule>;
