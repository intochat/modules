using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace IntoChat.OS.Modules.Flutter.Tests.Integration.Contracts;

// The W2 PR 0 pins read and (only through their explicit capture facts) rewrite files beside this project, found
// through the solution by project name, never by a path literal.
internal static class UiContractFiles
{
    private const string SolutionFile = "Intochat.OS.slnx";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Root { get; } = FindRoot();

    public static string DirectoryOf(string project)
    {
        var entry = XDocument.Load(Path.Combine(Root, SolutionFile)).Descendants("Project")
            .Select(element => element.Attribute("Path")!.Value.Replace('\\', '/'))
            .Single(path => Path.GetFileNameWithoutExtension(path) == project);
        return Path.GetFullPath(Path.Combine(Root, Path.GetDirectoryName(entry)!));
    }

    // Every project directory the solution lists (found through the solution, never by a path literal), plus the
    // single-file apps beside it.
    public static IEnumerable<string> ProductionSourceDirectories()
        => XDocument.Load(Path.Combine(Root, SolutionFile)).Descendants("Project")
            .Select(element => element.Attribute("Path")!.Value.Replace('\\', '/'))
            .Select(path => Path.GetFullPath(Path.Combine(Root, Path.GetDirectoryName(path)!)))
            .Append(Path.Combine(Root, "features"))
            .Where(Directory.Exists)
            .Distinct();

    public static string TestProject => DirectoryOf("IntoChat.OS.Modules.Flutter.Tests.Integration");

    // The Flutter workspace beside the module's projects: app/core, app/ui, app/shell.
    public static string Dart(string package, string file)
        => File.ReadAllText(Path.Combine(DirectoryOf("IntoChat.OS.Modules.Flutter"), "..", "..", "app", package, "lib/src", file));

    public static JsonObject Golden(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(TestProject, "Contracts", name)))!.AsObject();

    public static void Write(string relative, JsonNode node)
        => File.WriteAllText(Path.Combine(TestProject, relative), node.ToJsonString(Indented).ReplaceLineEndings("\n") + "\n");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFile))) { directory = directory.Parent; }
        return directory?.FullName ?? throw new InvalidOperationException($"{SolutionFile} was not found above {AppContext.BaseDirectory}.");
    }
}
