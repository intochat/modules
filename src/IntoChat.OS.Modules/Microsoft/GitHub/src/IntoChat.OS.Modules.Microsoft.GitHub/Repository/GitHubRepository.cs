namespace IntoChat.OS.Modules.Microsoft.GitHub;

// The coordinates a connection neuron holds; Key is the connection key that owns them.
[GenerateSerializer, Alias("github.repository-coordinates")]
public sealed record GitHubRepository(
    [property: Id(0)] string Key,
    [property: Id(1)] long AppId,
    [property: Id(2)] long InstallationId,
    [property: Id(3)] long RepositoryId,
    [property: Id(4)] string Owner,
    [property: Id(5)] string Name)
{
    public Uri ApiHost => new("https://api.github.com/");
    public string RepositoryPath => $"repos/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Name)}";
}
