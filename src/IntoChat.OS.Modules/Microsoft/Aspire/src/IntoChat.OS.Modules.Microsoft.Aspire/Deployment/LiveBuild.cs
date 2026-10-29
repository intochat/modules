using System.Text.Json;

namespace IntoChat.OS.Modules.Microsoft.Aspire;

/// <summary>The build the live silos run: its commit and published folder.</summary>
public sealed record LiveBuild(string Sha, string Path);

// {Workdir}/live.json names the build the live silos run; absent on first boot, when the AppHost runs from source.
/// <summary>Reads and writes {Workdir}/live.json; public for the module's Aspire hosting sibling.</summary>
public static class LiveBuildFile
{
    /// <summary>The live.json path under <paramref name="workdir"/>.</summary>
    public static string PathOf(string workdir) => System.IO.Path.Combine(workdir, "live.json");

    /// <summary>The live build, or null on first boot.</summary>
    public static LiveBuild? Read(string workdir)
    {
        var file = PathOf(workdir);
        return File.Exists(file) ? JsonSerializer.Deserialize<LiveBuild>(File.ReadAllText(file), Json) : null;
    }

    /// <summary>Names <paramref name="sha"/> as the live build; the build must already be published.</summary>
    public static void Write(string workdir, string sha)
    {
        var build = System.IO.Path.Combine(workdir, "builds", sha);
        if (!Directory.Exists(build)) { throw new DirectoryNotFoundException($"build {sha} was not published to {build}"); }
        var file = PathOf(workdir);
        var temporary = file + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new LiveBuild(sha, build), Json));
        File.Move(temporary, file, overwrite: true);
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
}
