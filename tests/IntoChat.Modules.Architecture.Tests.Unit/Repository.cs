namespace IntoChat.Modules.Architecture.Tests.Unit;

internal static class Repository
{
    internal const string SolutionFile = "IntoChat.Modules.slnx";

    internal static readonly string Root = FindRoot();

    internal static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFile)))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException($"{SolutionFile} not found above {AppContext.BaseDirectory}.");
    }
}
