using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Modules.Apps.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using DigitalBrain.Runtime.Orleans.Hosting;

namespace IntoChat.OS.Modules.AI.Routing;

// Fills the operation menu from the apps already installed, then keeps one app current when it changes.
// The scan runs at startup, outside a person's request. A route after that reads the menu.
// Apps are plain neurons (06-08): their AppChanged is heard on this silo through IClusterListeners and queued, so the refresh
// runs here, in order, outside the turn that published it.
internal sealed class AppMenuHost(DigitalBrain.Spi.IClusterListeners listeners, IServiceProvider plain, IKernel kernel,
    AppOperationCatalog catalog, ILogger<AppMenuHost> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var changes = System.Threading.Channels.Channel.CreateUnbounded<(string Key, string Version)>();
        IAsyncDisposable listening;
        try
        {
            listening = await listeners.Listen<AppChanged>(nameof(AppMenuHost), (publisher, change, _) =>
            {
                changes.Writer.TryWrite(($"{publisher.Brain.Value}/{publisher.Key}", change.Version));
                return Task.CompletedTask;
            });
        }
        // A host without the Apps module registers no app signals and has no apps to list.
        catch (ArgumentException) { return; }
        await using var _ = listening;
        await foreach (var (key, version) in changes.Reader.ReadAllAsync(stoppingToken))
        {
            try { await Refresh(key, version, stoppingToken); }
            catch (Exception error) when (error is not OperationCanceledException)
            { logger.LogWarning(error, "The app menu could not apply {App}.", key); }
        }
    }

    // The first change in a brain reads the whole menu. Later changes read the one app.
    // Both happen as apps are installed, not when a person asks to route a message.
    private async Task Refresh(string key, string version, CancellationToken cancellationToken)
    {
        var marker = key.IndexOf("/apps/", StringComparison.Ordinal);
        if (marker <= 0) { return; }
        var brain = key[..marker];
        // The first scan includes the changed app itself: a plain app announces its change before the kernel's directory may list
        // it (06-08: the directory registers a plain neuron once its call completes).
        if (!catalog.TryGet(brain, out _)) { await Scan(brain, key, version, cancellationToken); return; }
        if (IApp.NameOf(brain, key) is not { } name) { return; }
        if (await Operations(key, name, version, cancellationToken) is { } operations) { catalog.Upsert(brain, name, operations); }
    }

    private async Task Scan(string brain, string changed, string version, CancellationToken cancellationToken)
    {
        List<(string App, MenuOperation[] Operations)> apps = [];
        HashSet<string> keys = new(StringComparer.Ordinal);
        await foreach (var instance in kernel.InstancesIn(brain, kernel.TypeOf(typeof(IApp)), cancellationToken)) { keys.Add(instance.Key); }
        keys.Add(changed);
        foreach (var key in keys)
        {
            if (IApp.NameOf(brain, key) is not { } name) { continue; }
            if (await Operations(key, name, key == changed ? version : null, cancellationToken) is not { } operations) { return; }
            apps.Add((name, operations));
        }
        catalog.Replace(brain, apps);
    }

    // The app as of the change heard (version): a plain app's Read interleaves, so a read right after its change was announced may
    // still answer the state before it (06-08); it is read again, briefly, until it shows the announced version.
    private async Task<MenuOperation[]?> Operations(string key, string name, string? version, CancellationToken cancellationToken)
    {
        AppSnapshot snapshot;
        try
        {
            snapshot = await SiloNeurons.Get<IApp>(plain, key).Read().WaitAsync(cancellationToken);
            for (var attempt = 0; version is not null && snapshot.Version != version && attempt < 20; attempt++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                snapshot = await SiloNeurons.Get<IApp>(plain, key).Read().WaitAsync(cancellationToken);
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning(error, "The app menu could not read {App}.", name);
            return null;
        }
        if (snapshot.MigrationTarget is not null || snapshot.Manifest is null) { return []; }
        return snapshot.Manifest.Operations
            .Select(operation => new MenuOperation(name, operation.Name, string.IsNullOrWhiteSpace(operation.Description) ? null : operation.Description))
            .ToArray();
    }
}
