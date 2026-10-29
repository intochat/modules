using IntoChat.OS.Modules.Apps.Signals;
using IntoChat.OS.Modules.Circles;
using IntoChat.OS.Modules.Circles.Signals;
using IntoChat.OS.Modules.Flutter.Card.Signals;
using IntoChat.OS.Modules.Flutter.Person.Signals;
using IntoChat.OS.Modules.Flutter.Workspace.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using System.Collections.Concurrent;
using DigitalBrain.Runtime.Orleans.Signalling;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DigitalBrain.Runtime.Orleans.Hosting;

namespace IntoChat.OS.Modules.Flutter.Space;

// Rebuilds spaces when what they show changes. Signals of plain publishers (Card, Person; 06-04) arrive through IBrainListeners,
// per brain: a brain is followed when its Space activates on this silo, together with its friends, whose posts and profiles it
// shows. Workspaces, apps and circles (06-08) are heard on IClusterListeners for every brain this silo hosts; the local hub is
// gone (SpaceWatcherLegacyHubSignals.json is empty).
internal sealed class SpaceWatcher(ILogger<SpaceWatcher> logger, CircleMembers friends,
    IBrainListeners listeners, DigitalBrain.Spi.IClusterListeners cluster, IServiceProvider plain) : BackgroundService
{
    private readonly ConcurrentDictionary<string, Task> _followed = new(StringComparer.Ordinal);

    // Workspace and app changes come from plain workspaces and apps of every brain this silo hosts (IClusterListeners, R4-20;
    // apps 06-08). Each handler only posts a one-way recomposition, unstamped, so it is cheap in the publishing turn.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var workspaces = await cluster.Listen<WorkspaceChanged>(nameof(SpaceWatcher), (_, change, _) => Guarded(change, change => Rebuild(change.WorkspaceId)));
        // A host without the Apps module registers no app signals and has no apps to follow.
        await using var apps = await Optional(() => cluster.Listen<AppChanged>(nameof(SpaceWatcher), (publisher, change, _) => Guarded(change, _ => RefreshApp($"{publisher.Brain.Value}/{publisher.Key}"))));
        await using var presentations = await Optional(() => cluster.Listen<PresentationChanged>(nameof(SpaceWatcher), (_, change, _) => Guarded(change, change => RefreshApp(change.App))));
        // A host without the Circles module registers no circle signals and has no circles to follow.
        await using var revoked = await Optional(() => cluster.Listen<CircleMembershipRevoked>(nameof(SpaceWatcher), (_, change, _) => Guarded(change, change => RebuildMembers(change.PreviousMembers))));
        await using var circles = await Optional(() => cluster.Listen<CircleChanged>(nameof(SpaceWatcher), (_, change, _) => Guarded(change, change => RebuildMembers(change.Members.Concat(change.PreviousMembers ?? [])))));
        await using var posts = await Optional(() => cluster.Listen<CircleSpoke>(nameof(SpaceWatcher), (publisher, change, _) => Guarded(change, async _ =>
        {
            var circle = await Unstamped(() => SiloNeurons.Get<ICircleStateReader>(plain, ICircle.Key(publisher.Brain.Value)).Members());
            await RebuildMembers(circle);
        })));
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    // Follows brain and its friends on this silo, once each: their profile and post changes rebuild the spaces that show them.
    public async Task Follow(string brain)
    {
        if (!BrainScope.IsValidId(brain)) { return; }
        await Listen(brain);
        foreach (var friend in await friends.FriendsOf(brain)) { await Listen(friend.Brain); }
    }

    private Task Listen(string brain) => _followed.GetOrAdd(brain, async key =>
    {
        var id = new DigitalBrain.BrainId(key);
        await listeners.Listen<PersonChanged>(id, (change, _) => Guarded(change, change => RebuildAuthorAndFriends(change.Name.EndsWith("/profile", StringComparison.Ordinal) ? change.Name[..^8] : null)));
        await listeners.Listen<CardChanged>(id, (change, _) => Guarded(change, change => RebuildAuthorAndFriends(CircleMembers.PostBrain(change.Name))));
    });

    private static async Task<IAsyncDisposable> Optional(Func<Task<IAsyncDisposable>> listen)
    {
        try { return await listen(); }
        catch (ArgumentException) { return NoListener.Instance; }
    }

    private sealed class NoListener : IAsyncDisposable
    {
        public static readonly NoListener Instance = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private async Task Guarded<T>(T signal, Func<T, Task> handle)
    {
        try { await handle(signal); }
        catch (Exception error) when (error is not OperationCanceledException)
        { logger.LogWarning(error, "Could not rebuild a space after {Signal}.", typeof(T).Name); }
    }

    private async Task RebuildAuthorAndFriends(string? author)
    {
        if (!BrainScope.IsValidId(author)) { return; }
        await Rebuild(author);
        foreach (var friend in await friends.FriendsOf(author)) { await Rebuild(friend.Brain); }
    }

    private async Task RebuildMembers(IEnumerable<string> accounts)
    {
        foreach (var account in accounts.Distinct(StringComparer.Ordinal))
        { foreach (var brain in await friends.PersonalBrains(account)) { await Rebuild(brain); } }
    }

    private Task Rebuild(string? brain) => brain is null ? Task.CompletedTask : Unstamped(() => SiloNeurons.Get<ISpace>(plain, ISpace.Key(brain)).Recompose());
    private Task RefreshApp(string key) => BrainOf(key) is { } brain ? Unstamped(() => SiloNeurons.Get<ISpace>(plain, ISpace.Key(brain)).RefreshApp(key)) : Task.CompletedTask;

    // A recomposition is the module's own act. A plain listener runs in the turn that saved the change, with that writer's stamp,
    // and a friend's change must not reach another brain's space as the friend: the call goes out unstamped, as the hub pump's did.
    private static async Task Unstamped(Func<Task> call)
    {
        Orleans.Runtime.RequestContext.Remove(CallerContextStamper.RequestContextKey);
        await call();
    }

    private static async Task<T> Unstamped<T>(Func<Task<T>> call)
    {
        Orleans.Runtime.RequestContext.Remove(CallerContextStamper.RequestContextKey);
        return await call();
    }
    private static string? BrainOf(string key)
    {
        var marker = key.IndexOf("/apps/", StringComparison.Ordinal);
        return marker > 0 ? key[..marker] : null;
    }
}
