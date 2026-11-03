using System.Text.Json;
using System.Xml.Linq;

namespace IntoChat.Modules.Architecture.Tests.Unit;

public sealed class BoundaryFacts
{
    [Fact]
    public void NoModuleClosureReachesAnOsAssembly()
    {
        var projects = Directory.EnumerateFiles(Path.Combine(Repository.Root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(path =>
            {
                var relative = Repository.Relative(path);
                return relative.StartsWith("src/", StringComparison.Ordinal)
                    && relative.Contains("/src/", StringComparison.Ordinal)
                    && !relative.Split('/').Any(part => part is "obj" or "bin");
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(projects);

        var offenders = new List<string>();
        foreach (var csprojPath in projects)
        {
            var assetsPath = ResolveAssetsPath(csprojPath);
            Assert.True(File.Exists(assetsPath), $"{assetsPath} is missing; restore {Path.GetFileNameWithoutExtension(csprojPath)} first.");

            using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
            var osPackages = new List<string>();
            foreach (var library in document.RootElement.GetProperty("libraries").EnumerateObject())
            {
                var slash = library.Name.IndexOf('/', StringComparison.Ordinal);
                var name = slash < 0 ? library.Name : library.Name[..slash];
                var isOsId = name.Equals("IntoChat.OS", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("IntoChat.OS.", StringComparison.OrdinalIgnoreCase);
                if (!isOsId)
                {
                    continue;
                }

                var type = library.Value.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
                if (string.Equals(type, "package", StringComparison.OrdinalIgnoreCase)
                    || !name.StartsWith("IntoChat.OS.Modules.", StringComparison.OrdinalIgnoreCase))
                {
                    osPackages.Add(name);
                }
            }

            if (osPackages.Count > 0)
            {
                offenders.Add($"{Path.GetFileNameWithoutExtension(csprojPath)}: {string.Join(", ", osPackages.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))}");
            }
        }

        Assert.True(offenders.Count == 0, "Module restore closure reaches IntoChat.OS packages:\n" + string.Join('\n', offenders));
    }

    [Fact]
    public void Sources_are_remote_only()
    {
        var directory = new DirectoryInfo(Repository.Root);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nuget.config")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var config = XDocument.Load(Path.Combine(directory.FullName, "nuget.config"));
        var sources = config.Descendants("packageSources").Elements("add").Select(e => e.Attribute("value")!.Value).ToList();

        Assert.NotEmpty(sources);
        Assert.NotEmpty(config.Descendants("packageSources").Elements("clear"));
        Assert.All(sources, value =>
        {
            Assert.StartsWith("https://", value, StringComparison.Ordinal);
            Assert.DoesNotContain("local-feed", value, StringComparison.OrdinalIgnoreCase);
        });
        Assert.NotEmpty(config.Descendants("packageSourceMapping"));
    }

    private static string ResolveAssetsPath(string csprojPath)
    {
        var artifactsAssets = Path.Combine(
            Repository.Root,
            "artifacts",
            "obj",
            Path.GetFileNameWithoutExtension(csprojPath),
            "project.assets.json");
        if (File.Exists(artifactsAssets))
        {
            return artifactsAssets;
        }

        return Path.Combine(Path.GetDirectoryName(csprojPath)!, "obj", "project.assets.json");
    }
}
