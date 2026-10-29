using IntoChat.OS.Runtime;
using DigitalBrain.Spi;
using DigitalBrain;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Modules.Flutter.Space;
using IntoChat.OS.Modules.Flutter.Workspace.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using Microsoft.Extensions.DependencyInjection;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Workspace;

[Neuron("ui.workspace", State = "workspace")]
internal sealed partial class WorkspaceNeuron
    : UiNeuron<WorkspaceStorage>, IWorkspace
{
    // The brain's one workspace, keyed {brain}/main (R4-19); its signals and paths name the brain.
    private string Brain => Id.Brain.Value;

    // False when the operation was already applied: a replay changes nothing and announces nothing.
    private async Task<bool> Commit(string operationId, long expectedRevision, Func<WorkspaceStorage, WorkspaceStorage> change, Func<WorkspaceStorage, DigitalBrain.Signal> changed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (Snapshot.Operations.Contains(operationId, StringComparer.Ordinal)) { return false; }
        if (expectedRevision != Snapshot.Revision)
        { throw new RevisionConflictException($"Expected revision {expectedRevision}; current revision is {Snapshot.Revision}.", Snapshot.Revision); }
        var next = change(Snapshot).Advanced(operationId);
        await Save(next, changed(next));
        return true;
    }

    public async Task<WorkspaceState> Apply(WorkspaceUpdate next, string operationId, long expectedRevision)
    {
        if (!await ShippedCaller("workspace", "desktop"))
        { throw new UnauthorizedAccessException("Only the shipped workspace and desktop apps may apply navigation state."); }
        ArgumentNullException.ThrowIfNull(next);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (operationId.Length > 256) { throw new ArgumentException("An operation id has at most 256 characters."); }
        CheckViews(next.OpenViews);
        CheckViews(next.Pins);
        if (next.FocusedViewId is { } focus && !next.OpenViews.Any(view => view.Id == focus))
        { throw new ArgumentException("Focus must name an open view."); }
        if (next.Windows is { } windows) { CheckWindows(windows, next.OpenViews); }
        if (next.Favourites is { } favourites) { CheckFavourites(favourites); }
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(next))));
        var receipt = Snapshot.Receipts.FirstOrDefault(item => item.Id == operationId);
        if (receipt is not null)
        {
            if (receipt.Digest != digest) { throw new ArgumentException("An operation id cannot be reused for another update."); }
            return await Read();
        }
        await Commit(operationId, expectedRevision, state => state with
        {
            OpenViews = next.OpenViews,
            FocusedViewId = next.FocusedViewId,
            Pins = next.Pins,
            Windows = next.Windows ?? WorkspaceWindows.Reconcile(
                state.Windows is null or [] ? WorkspaceWindows.Derive(state.OpenViews ?? []) : state.Windows, next.OpenViews),
            Favourites = next.Favourites ?? state.Favourites ?? [],
            Path = [Brain],
            Open = [],
            Receipts = [.. state.Receipts.TakeLast(63), new WorkspaceReceipt(operationId, digest)],
        }, state => new WorkspaceChanged(Brain, state.Revision));
        return await Read();
    }

    // The caller is one of the named apps of this brain, and that app is the one the OS ships, not one
    // that merely bears its name.
    private async Task<bool> ShippedCaller(params string[] names)
    {
        if (!CallerContextStamper.TryGet(out var caller) || caller.Kind != CallerKind.App || caller.AppId is not { } app) { return false; }
        if (names.FirstOrDefault(name => app == IApp.Key(Brain, name)) is not { } named) { return false; }
        return ShippedApps.Is(await ServiceProvider.GetRequiredService<INeuronProxies>().Get<IApp>(app).Read(), named);
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,64}$")]
    private static partial Regex WindowId();

    private static void CheckWindows(WorkspaceWindow[] windows, WorkspaceViewRef[] openViews)
    {
        if (windows.Length > 8 || windows.Any(window => window is null || window.Tabs is null || window.Tabs.Length is 0 or > 8
            || window.Id is null || !WindowId().IsMatch(window.Id) || window.Tabs.Any(tab => tab is null)
            || !window.Tabs.Any(tab => tab.Id == window.ActiveTabId)))
        { throw new ArgumentException("Provide at most 8 windows, each with an id, 1 to 8 tabs and an active tab among them."); }
        if (windows.Select(window => window.Id).Distinct(StringComparer.Ordinal).Count() != windows.Length)
        { throw new ArgumentException("Window ids are unique."); }
        var tabs = windows.SelectMany(window => window.Tabs).ToArray();
        var open = openViews.ToDictionary(view => view.Id, StringComparer.Ordinal);
        // Each tab is its open view exactly (app, variant and context too), so a tab cannot bypass CheckViews.
        if (tabs.Select(tab => tab.Id).Distinct(StringComparer.Ordinal).Count() != tabs.Length || tabs.Length != open.Count
            || tabs.Any(tab => !open.TryGetValue(tab.Id, out var view) || view != tab))
        { throw new ArgumentException("Each open view is exactly one tab of one window."); }
    }

    private void CheckFavourites(WorkspaceViewRef[] favourites)
    {
        CheckViews(favourites, allowAppOnly: true);
        if (favourites.Count(view => view.Variant.Length == 0) > 5 || favourites.Count(view => view.Variant.Length > 0) > 5)
        { throw new ArgumentException("Favourites hold at most 5 apps and 5 views."); }
    }

    private void CheckViews(WorkspaceViewRef[] views, bool allowAppOnly = false)
    {
        ArgumentNullException.ThrowIfNull(views);
        if (views.Length > 64 || views.Any(view => view is null || string.IsNullOrWhiteSpace(view.Id) || view.Id.Length > 128
            || view.Variant is null || (view.Variant.Length == 0 ? !allowAppOnly : string.IsNullOrWhiteSpace(view.Variant)) || view.Variant.Length > 64 || view.Context is null || view.Context.Length > 1024
            || string.IsNullOrWhiteSpace(view.App) || view.App.Length > 512 || !IsApp(view.App)
            || view.App.Split('/').Any(part => part.Length == 0 || part is "." or ".."))
            || views.Select(view => view.Id).Distinct(StringComparer.Ordinal).Count() != views.Length)
        { throw new ArgumentException("Provide at most 64 uniquely identified views of apps in this brain."); }
    }
    private WorkspaceStorage Seed(WorkspaceStorage state) => state.Path.Length == 0
        ? state with { Path = [Brain, Brain + "/apps/ino"], Open = [Brain + "/apps/ino"] } : state;

    public Task<WorkspaceState> Show(string app, string operationId, long expectedRevision)
    {
        ValidateKey(app);
        return Change(operationId, expectedRevision, state => state with
        {
            Path = state.Path[^1] == app ? state.Path : [.. state.Path, app],
            Open = IsApp(app) && !state.Open.Contains(app, StringComparer.Ordinal) ? [.. state.Open, app] : state.Open,
        });
    }

    public Task<WorkspaceState> Back(string operationId, long expectedRevision)
        => Change(operationId, expectedRevision, state => state with { Path = state.Path.Length > 1 ? state.Path[..^1] : state.Path });

    public Task<WorkspaceState> Home(string operationId, long expectedRevision)
        => Change(operationId, expectedRevision, state => state with { Path = [Brain] });

    public Task<WorkspaceState> Close(string app, string operationId, long expectedRevision)
    {
        ValidateKey(app);
        return Change(operationId, expectedRevision, state =>
        {
            var at = Array.FindIndex(state.Path, key => key == app || key.StartsWith(app + "/", StringComparison.Ordinal));
            return state with
            {
                Open = [.. state.Open.Where(key => key != app)],
                Path = at > 0 ? state.Path[..at] : state.Path,
            };
        });
    }

    public Task<WorkspaceState> Read()
    {
        var state = Seed(Snapshot);
        return Task.FromResult(new WorkspaceState(state.Revision, state.Path, state.Open)
        {
            OpenViews = state.OpenViews,
            FocusedViewId = state.FocusedViewId,
            Pins = state.Pins,
            Windows = state.Windows is null or [] ? WorkspaceWindows.Derive(state.OpenViews ?? []) : state.Windows,
            Favourites = state.Favourites ?? [],
        });
    }

    private async Task<WorkspaceState> Change(string operationId, long expectedRevision, Func<WorkspaceStorage, WorkspaceStorage> change)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (operationId.Length > 256) { throw new ArgumentException("An operation id has at most 256 characters.", nameof(operationId)); }
        if (await Commit(operationId, expectedRevision, state => change(Seed(state)), state => new WorkspaceChanged(Brain, state.Revision)))
        { await Get<ISpace>(new DigitalBrain.NeuronId(Id.Brain, ISpace.Type, "main")).Recompose(); }
        return await Read();
    }

    private bool IsApp(string key) => key.StartsWith(Brain + "/apps/", StringComparison.Ordinal) && !key[(Brain.Length + 6)..].Contains('/');

    private void ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var segments = key.Split('/');
        if (key.Length > 512 || segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        { throw new ArgumentException("Provide a safe neuron key.", nameof(key)); }
        var friend = key.EndsWith("/profile", StringComparison.Ordinal) || key.Contains("/posts/", StringComparison.Ordinal);
        if (!key.StartsWith(Brain + "/", StringComparison.Ordinal) && !friend)
        { throw new ArgumentException("A workspace shows its own neurons or a member profile or post.", nameof(key)); }
    }
}

[GenerateSerializer, Alias("ui.workspace-storage")]
[ExportableState]
internal sealed record WorkspaceStorage
{
    [Id(0)] public long Revision { get; init; }
    [Id(1)] public string[] Operations { get; init; } = [];
    [Id(2)] public string[] Path { get; init; } = [];
    [Id(3)] public string[] Open { get; init; } = [];
    [Id(4)] public WorkspaceViewRef[] OpenViews { get; init; } = [];
    [Id(5)] public string? FocusedViewId { get; init; }
    [Id(6)] public WorkspaceViewRef[] Pins { get; init; } = [];
    [Id(7)] public WorkspaceReceipt[] Receipts { get; init; } = [];
    [Id(8)] public WorkspaceWindow[] Windows { get; init; } = [];
    [Id(9)] public WorkspaceViewRef[] Favourites { get; init; } = [];
    public WorkspaceStorage Advanced(string operationId) => this with
    { Revision = checked(Revision + 1), Operations = [.. Operations.TakeLast(63), operationId] };
}

[GenerateSerializer, Alias("ui.workspace-receipt")]
internal sealed record WorkspaceReceipt([property: Id(0)] string Id, [property: Id(1)] string Digest);
