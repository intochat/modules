using System.Diagnostics;
using System.Xml.Linq;

namespace IntoChat.Modules.Architecture.Tests.Unit;

public sealed class BumpPinsFacts
{
    private static string ScriptPath => Path.Combine(Repository.Root, "scripts", "bump-pins.cs");

    private static string FixturePath => Path.Combine(
        Repository.Root,
        "tests",
        "IntoChat.Modules.Architecture.Tests.Unit",
        "fixtures",
        "Directory.Packages.fixture.props");

    [Fact]
    public async Task RewritesEveryPinOfTheFamily()
    {
        var tempFile = CopyFixtureToTemp();
        try
        {
            var exitCode = await RunAsync("DigitalBrain", "0.13.0-preview.12", tempFile);
            Assert.Equal(0, exitCode);

            var pins = ReadPins(tempFile);
            Assert.Equal("0.13.0-preview.12", pins["DigitalBrain"]);
            Assert.Equal("0.13.0-preview.12", pins["DigitalBrain.UI"]);
            Assert.Equal("0.13.0-preview.12", pins["DigitalBrain.Runtime.Orleans"]);
            Assert.Equal("0.12.0-preview.4", pins["IntoChat.OS.Modules.AI"]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExitsTwoWhenNothingChanges()
    {
        var tempFile = CopyFixtureToTemp();
        try
        {
            var firstExit = await RunAsync("DigitalBrain", "0.13.0-preview.12", tempFile);
            Assert.Equal(0, firstExit);

            var secondExit = await RunAsync("DigitalBrain", "0.13.0-preview.12", tempFile);
            Assert.Equal(2, secondExit);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RejectsAVersionThatIsNotSemver()
    {
        var tempFile = CopyFixtureToTemp();
        try
        {
            var exitCode = await RunAsync("DigitalBrain", "latest", tempFile);
            Assert.Equal(1, exitCode);

            var pins = ReadPins(tempFile);
            Assert.Equal("0.12.0-preview.2", pins["DigitalBrain"]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static string CopyFixtureToTemp()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"bump-pins-{Guid.NewGuid():N}.props");
        File.Copy(FixturePath, tempFile, overwrite: true);
        return tempFile;
    }

    private static Dictionary<string, string> ReadPins(string path)
        => XDocument.Load(path)
            .Descendants("PackageVersion")
            .ToDictionary(
                element => element.Attribute("Include")!.Value,
                element => element.Attribute("Version")!.Value,
                StringComparer.Ordinal);

    private static async Task<int> RunAsync(string family, string version, string propsPath)
    {
        Assert.True(File.Exists(ScriptPath), $"Missing script: {ScriptPath}");

        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Repository.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("scripts/bump-pins.cs");
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("--family");
        start.ArgumentList.Add(family);
        start.ArgumentList.Add("--version");
        start.ArgumentList.Add(version);
        start.ArgumentList.Add("--props");
        start.ArgumentList.Add(propsPath);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start dotnet run.");
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return process.ExitCode;
    }
}
