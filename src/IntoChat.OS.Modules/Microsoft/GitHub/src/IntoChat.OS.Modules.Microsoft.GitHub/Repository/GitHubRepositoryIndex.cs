using DigitalBrain.Contracts;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Microsoft.GitHub;

// The connection keys a webhook for one installation reaches, across every brain. A cluster-scoped singleton per installation
// (R4-19): since 06-06d a plain neuron in the reserved system brain, "system/installation/{installationId}" (clean break under
// SAFE-01, D-17: a connection that connects again records itself). Module plumbing: no edge names it, and the owner rule does
// not apply to an internal contract, so a connection of any brain and the webhook ingress reach it.
[NeuronAlias("github.repository-index"), DefaultNeuronType(GitHubRepositoryIndex.Type)]
internal interface IGitHubRepositoryIndex
{
    Task Add(string connectionKey);
    Task Remove(string connectionKey);
    [DigitalBrain.ReadOnlyCall] Task<string[]> Read();

    static string Key(long installationId) => $"{SystemBrain}/installation/{installationId}";

    const string SystemBrain = "system";
}

[ExportableState]
internal sealed record GitHubRepositoryIndexState { public string[] Keys { get; init; } = []; }

// The index after a change (a plain save publishes what changed).
[DigitalBrain.Signal("github.repository-index-changed")]
internal sealed record GitHubRepositoryIndexChanged(string[] Keys) : DigitalBrain.Signal;

[DigitalBrain.Neuron(Type, State = "github-repository-index")]
internal sealed class GitHubRepositoryIndex : DigitalBrain.Neuron<GitHubRepositoryIndexState>, IGitHubRepositoryIndex
{
    public const string Type = "github.repository-index";

    public Task<string[]> Read() => Task.FromResult(State.Keys);

    public Task Add(string connectionKey) => Keep([.. State.Keys.Append(connectionKey).Distinct(StringComparer.Ordinal)]);

    public Task Remove(string connectionKey) => Keep([.. State.Keys.Where(key => key != connectionKey)]);

    private Task Keep(string[] keys)
        => keys.SequenceEqual(State.Keys, StringComparer.Ordinal) ? Task.CompletedTask : Save(State with { Keys = keys }, new GitHubRepositoryIndexChanged(keys));
}
