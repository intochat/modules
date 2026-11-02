using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using DigitalBrain;
using DigitalBrain.UI;
using IntoChat.OS.Client;
using IntoChat.OS.Client.Runtime;
using IntoChat.OS.Client.Runtime.V1;
using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Modules.Apps.Signals;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

// The lifecycle manager and execution runner of a program that is a framework [App] class:
// it downloads, caches, loads the assembly in a collectible AssemblyLoadContext, and runs
// the app's turn and signal loops against IntoChatOS.
public sealed class AppRunner : IAsyncDisposable
{
    private static readonly TimeSpan StopGrace = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CacheGates = new(StringComparer.Ordinal);

    private readonly CancellationTokenSource _stopping = new();
    private readonly RunLog _log;
    private readonly Type _appType;
    private AssemblyLoadContext? _context;
    private long _stateRevision;
    private string? _lastSavedState;

    internal AppRunner(AssignmentEvent assignment, AssemblyLoadContext context, Type appType, int logLines)
    {
        Assignment = assignment;
        Id = assignment.Behavior;
        Hash = assignment.Hash;
        _context = context;
        _appType = appType;
        _log = new RunLog(logLines);
        Completion = Task.FromResult(0);
    }

    public AssignmentEvent Assignment { get; }
    public string Id { get; }
    public string Hash { get; }
    public Task<int> Completion { get; private set; }
    public string? Error { get; private set; }
    public AssemblyLoadContext? Context => _context;
    public Type AppType => _appType;

    public string Tail(int lines) => _log.Tail(lines);
    public void Note(string line) => _log.Append(line);
    public bool Draining { get; internal set; }

    public static Type? AppTypeIn(Assembly assembly)
    {
        var apps = assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false } && type.IsDefined(typeof(AppAttribute), false)).ToArray();
        return apps.Length == 1 ? apps[0] : null;
    }

    public static async Task PrepareAsync(HttpClient http, IOptions<SandboxOptions> options, AssignmentEvent assignment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        await CachedAsync(http, options.Value.CacheRoot, assignment.Hash, assignment.Assembly, verify: true, cancellationToken).ConfigureAwait(false);
        await CachedAsync(http, options.Value.CacheRoot, assignment.Hash + ".pdb", assignment.Symbols, verify: false, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<AppRunner> LoadAsync(HttpClient http, IOptions<SandboxOptions> options, SessionConnection session, AssignmentEvent assignment, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        var assemblyBytes = await CachedAsync(http, options.Value.CacheRoot, assignment.Hash, assignment.Assembly, verify: true, cancellationToken).ConfigureAwait(false);
        var symbolBytes = await CachedAsync(http, options.Value.CacheRoot, assignment.Hash + ".pdb", assignment.Symbols, verify: false, cancellationToken).ConfigureAwait(false);
        return Start(session, assignment, assemblyBytes, symbolBytes, options.Value.LogLines, logger);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static AppRunner Start(SessionConnection session, AssignmentEvent assignment, byte[] assemblyBytes, byte[] symbolBytes, int logLines, ILogger logger)
    {
        var (context, assembly) = LoadAssembly(assignment, assemblyBytes, symbolBytes);
        var appType = AppTypeIn(assembly) ?? throw new InvalidOperationException($"{assignment.Behavior} does not declare exactly one [App] class.");
        var runner = new AppRunner(assignment, context, appType, logLines);
        runner.Completion = Task.Run(() => runner.ExecuteAsync(session, assignment.Settings, logger));
        return runner;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (AssemblyLoadContext Context, Assembly Assembly) LoadAssembly(AssignmentEvent assignment, byte[] assemblyBytes, byte[] symbolBytes)
    {
        var context = new AppLoadContext(assignment.Behavior);
        try
        {
            using var image = new MemoryStream(assemblyBytes);
            using var pdb = new MemoryStream(symbolBytes);
            var loaded = symbolBytes.Length == 0 ? context.LoadFromStream(image) : context.LoadFromStream(image, pdb);
            return (context, loaded);
        }
        catch
        {
            context.Unload();
            throw;
        }
    }

    internal static async Task<byte[]> CachedAsync(HttpClient http, string cacheRoot, string name, Uri url, bool verify, CancellationToken cancellationToken)
    {
        var path = Path.Combine(cacheRoot, name);
        if (File.Exists(path))
        {
            try
            {
                var cached = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                if (!verify || Matches(name, cached)) { return cached; }
            }
            catch (IOException)
            {
                // In-flight write/move collision in fast path; fall through to synchronized block.
            }
        }

        var gate = CacheGates.GetOrAdd(name, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(path))
            {
                var cached = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                if (!verify || Matches(name, cached)) { return cached; }
            }

            var bytes = await http.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
            if (verify && !Matches(name, bytes)) { throw new InvalidDataException($"The assembly downloaded for {name} does not hash to its name; refusing to load it."); }
            Directory.CreateDirectory(cacheRoot);
            var tempFile = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllBytesAsync(tempFile, bytes, cancellationToken).ConfigureAwait(false);
                File.Move(tempFile, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { /* best effort cleanup */ }
                }
            }

            return bytes;
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool Matches(string hash, byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes)) == hash;

    private async Task<int> ExecuteAsync(SessionConnection session, Dictionary<string, string> settings, ILogger logger)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["Behavior"] = Id, ["Hash"] = Hash });
        using var ambient = AmbientSession.Enter(session, Id, settings, _stopping.Token, Hash, Assignment.Revision);
        using var output = BehaviorConsole.Capture(line => { _log.Append(line); logger.LogInformation("{Behavior}: {Line}", Id, line); });
        try
        {
            return await RunAsync(_stopping.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (Unwrap(error) is OperationCanceledException && _stopping.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception error)
        {
            Error = Unwrap(error).ToString();
            _log.Append(Error);
            logger.LogWarning(Unwrap(error), "Behavior {Behavior} {Hash} failed", Id, Hash);
            return 1;
        }
    }

    private static Exception Unwrap(Exception error) => error is TargetInvocationException { InnerException: { } inner } ? inner : error;

    public static async Task<int> RunAsync(Type appType, CancellationToken stopping)
    {
        var runner = new AppRunner(new("direct", "", new Uri("about:blank"), new Uri("about:blank"), new Dictionary<string, string>()), AssemblyLoadContext.Default, appType, 100);
        return await runner.RunAsync(stopping).ConfigureAwait(false);
    }

    public async Task<int> RunAsync(CancellationToken stopping)
    {
        await using var brain = await IntoChatOSClient.ConnectAsync([], stopping).ConfigureAwait(false);
        var appKey = brain.Setting("App") ?? throw new InvalidOperationException("An [App] program is assigned with the Setting \"App\": its app key.");
        var definition = new AppDefinition(_appType);
        var presenter = new ViewPresenter(brain, appKey, long.Parse(brain.Setting("ProgramRevision") ?? "1", System.Globalization.CultureInfo.InvariantCulture));
        var instance = definition.Create(service => service == typeof(IPresenter) ? presenter : null);
        instance.AttachNeuronResolver((contractType, key) => ResolveNeuron(brain, contractType, key));
        var app = brain.Get<IApp>(appKey);
        var snapshot = await app.Read().ConfigureAwait(false);
        if (instance.GetType().GetMethod("AttachCapabilityResolver", [typeof(Func<Type, object?>)]) is { } attachResolver)
        {
            attachResolver.Invoke(instance, [new Func<Type, object?>(capabilityType => ResolveCapability(brain, snapshot, capabilityType))]);
        }
        if (snapshot.GetType().GetProperty("RuntimeStateRevision")?.GetValue(snapshot) is long rev) { _stateRevision = rev; }
        // A state that cannot be restored stops the app before any turn runs: a turn would save the default state over it.
        if (snapshot.GetType().GetProperty("RuntimeStateJson")?.GetValue(snapshot) is string stateJson) { _lastSavedState = RestoreState(instance, stateJson); }
        RefreshViews(instance, presenter);
        var turns = new SemaphoreSlim(1, 1);
        var operationLoops = ServedOperations(definition, snapshot.Manifest.Operations.Select(operation => operation.Name))
            .Select(operation => Serve(brain, app, definition, instance, presenter, operation, turns, stopping));
        var signalLoops = definition.SignalSubscriptions
            .Select(sub => ServeSignalSubscription(brain, app, definition, instance, presenter, sub, turns, stopping));
        var address = AddressOf(appKey, brain.Setting("Brain"));
        var watchLoops = instance.DeclaredWatches()
            .Select(watch => ServeWatch(brain, app, definition, instance, presenter, watch, address, turns, stopping));
        var loops = operationLoops.Concat(signalLoops).Concat(watchLoops).ToArray();
        try { await Task.WhenAll(loops).ConfigureAwait(false); }
        finally { await presenter.DrainAsync().ConfigureAwait(false); }
        return 0;
    }

    private static object ResolveNeuron(IntoChatOSConnection brain, Type contractType, string key)
        => typeof(IntoChatOSConnection)
            .GetMethod(key.Contains('/') ? nameof(IntoChatOSConnection.Get) : nameof(IntoChatOSConnection.Neuron), [typeof(string)])!
            .MakeGenericMethod(contractType)
            .Invoke(brain, [key])!;

    internal static object ResolveCapability(IntoChatOSConnection brain, AppSnapshot snapshot, Type capabilityType)
    {
        var capabilityAttr = capabilityType.GetCustomAttribute<DigitalBrain.CapabilityAttribute>(false);
        var capabilityName = capabilityAttr?.Name
            ?? capabilityType.GetCustomAttribute<DigitalBrain.Contracts.NeuronAliasAttribute>(false)?.Alias
            ?? capabilityType.GetCustomAttribute<DigitalBrain.Contracts.DefaultNeuronTypeAttribute>(false)?.Type
            ?? capabilityType.Name.ToLowerInvariant();
        var capabilityVersion = capabilityAttr?.Version ?? 1;
        var capabilityId = new DigitalBrain.Capabilities.CapabilityId(capabilityName, capabilityVersion);

        if (snapshot.Manifest.Requires is not null)
        {
            var declared = snapshot.Manifest.Requires.Capabilities ?? [];
            var isConsented = declared.Any(id => DigitalBrain.Capabilities.CapabilityId.TryParse(id, out var parsed) && parsed.Satisfies(capabilityId));
            if (!isConsented)
            {
                throw new UnauthorizedAccessException(
                    $"Security Violation: App '{snapshot.Manifest.Title}' attempted to access capability '{capabilityId}' without declaring it in [AppManifest.Requires.Capabilities].");
            }
        }

        if (capabilityType == typeof(DigitalBrain.Capabilities.Storage.IStorage))
        {
            var storageKey = brain.Setting("DefaultStorageConnection") ?? brain.Setting("StorageConnection");
            return storageKey is not null
                ? brain.Get<DigitalBrain.Capabilities.Storage.IStorage>(storageKey)
                : brain.Neuron<DigitalBrain.Capabilities.Storage.IStorage>("local-disk/main");
        }

        return ResolveNeuron(brain, capabilityType, capabilityName);
    }

    private Task ServeSignalSubscription(
        IntoChatOSConnection brain,
        IApp app,
        AppDefinition definition,
        App instance,
        IPresenter presenter,
        AppSignalSubscription sub,
        SemaphoreSlim turns,
        CancellationToken stopping)
        => (Task)typeof(AppRunner)
            .GetMethod(nameof(ServeSignalGeneric), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(sub.SignalType)
            .Invoke(this, [brain, app, definition, instance, presenter, sub, turns, stopping])!;

    private async Task ServeSignalGeneric<TSignal>(
        IntoChatOSConnection brain,
        IApp app,
        AppDefinition definition,
        App instance,
        IPresenter presenter,
        AppSignalSubscription sub,
        SemaphoreSlim turns,
        CancellationToken stopping) where TSignal : Signal
    {
        try
        {
            var contractType = sub.ContractType ?? typeof(IntoChat.OS.IBrain);
            var sourceNeuron = ResolveNeuron(brain, contractType, sub.SourceKey);
            await foreach (var signal in brain.On<TSignal>(sourceNeuron, stopping).ConfigureAwait(false))
            {
                await turns.WaitAsync(stopping).ConfigureAwait(false);
                try
                {
                    await definition.Invoke(instance, sub.OperationName, signal, Json, stopping).ConfigureAwait(false);
                    RefreshViews(instance, presenter);
                    await PersistStateAsync(app, instance).ConfigureAwait(false);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    AppLog.Log($"{definition.Id}.{sub.OperationName} failed for signal {typeof(TSignal).Name}: {error}");
                }
                finally
                {
                    turns.Release();
                }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
    }

    // The brain an app runs in is its Brain setting, or the scope its key ({brain}/apps/{name}) names.
    internal static AppAddress AddressOf(string appKey, string? brainSetting)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appKey);
        if (!string.IsNullOrWhiteSpace(brainSetting)) { return new(brainSetting, appKey); }
        var at = appKey.IndexOf("/apps/", StringComparison.Ordinal);
        return at > 0 ? new(appKey[..at], appKey) : throw new InvalidOperationException($"{appKey} is not an app key ({{brain}}/apps/{{name}}).");
    }

    // A declared watch: one neuron, or the members its registry names (selected again on each registry change). Each signal is a turn.
    private Task ServeWatch(IntoChatOSConnection brain, IApp app, AppDefinition definition, App instance, IPresenter presenter, AppWatch watch,
        AppAddress address, SemaphoreSlim turns, CancellationToken stopping)
    {
        var source = ResolveNeuron(brain, watch.SourceType, watch.Key(address));
        if (!watch.IsSet)
        {
            return (Task)typeof(AppRunner).GetMethod(nameof(ServeFixedWatch), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(watch.SignalType)
                .Invoke(this, [brain, app, definition, instance, presenter, watch, source, turns, stopping])!;
        }
        return (Task)typeof(AppRunner).GetMethod(nameof(ServeSetWatch), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(watch.SourceType, watch.ChangeType!, watch.MemberType!, watch.SignalType)
            .Invoke(this, [brain, app, definition, instance, presenter, watch, source, address, turns, stopping])!;
    }

    private Task ServeFixedWatch<TSignal>(IntoChatOSConnection brain, IApp app, AppDefinition definition, App instance, IPresenter presenter,
        AppWatch watch, object source, SemaphoreSlim turns, CancellationToken stopping) where TSignal : Signal
        => ServeWatchSignals(brain.On<TSignal>(source, stopping), app, definition, instance, presenter, watch, turns, stopping);

    private Task ServeSetWatch<TRegistry, TChange, TMember, TSignal>(IntoChatOSConnection brain, IApp app, AppDefinition definition, App instance,
        IPresenter presenter, AppWatch watch, object source, AppAddress address, SemaphoreSlim turns, CancellationToken stopping)
        where TRegistry : class where TChange : Signal where TMember : class where TSignal : Signal
    {
        var sources = Sources.Named<TRegistry, TChange, TMember>((TRegistry)source, async (registry, ct) =>
            [.. (await watch.Select!(registry, address, ct).ConfigureAwait(false)).Select(key => (TMember)ResolveNeuron(brain, typeof(TMember), key))]);
        return ServeWatchSignals(brain.On<TSignal>(sources, stopping), app, definition, instance, presenter, watch, turns, stopping);
    }

    private async Task ServeWatchSignals<TSignal>(IAsyncEnumerable<TSignal> signals, IApp app, AppDefinition definition, App instance,
        IPresenter presenter, AppWatch watch, SemaphoreSlim turns, CancellationToken stopping) where TSignal : Signal
    {
        try
        {
            await foreach (var signal in signals.WithCancellation(stopping).ConfigureAwait(false))
            {
                await turns.WaitAsync(stopping).ConfigureAwait(false);
                try
                {
                    await watch.Handle(signal).ConfigureAwait(false);
                    RefreshViews(instance, presenter);
                    await PersistStateAsync(app, instance).ConfigureAwait(false);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    AppLog.Log($"{definition.Id}.{watch.Name} failed for signal {typeof(TSignal).Name}: {error}");
                }
                finally { turns.Release(); }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
    }

    private async Task Serve(IntoChatOSConnection brain, IApp app, AppDefinition definition, App instance, IPresenter presenter, string operation, SemaphoreSlim turns, CancellationToken stopping)
    {
        try
        {
            await foreach (var request in brain.On(app, operation, stopping).ConfigureAwait(false))
            {
                await turns.WaitAsync(stopping).ConfigureAwait(false);
                try { await Turn(app, definition, instance, presenter, operation, request, stopping).ConfigureAwait(false); }
                finally { turns.Release(); }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
    }

    private async Task Turn(IApp app, AppDefinition definition, App instance, IPresenter presenter, string operation, AppRequested request, CancellationToken stopping)
    {
        try
        {
            await definition.Invoke(instance, operation, Argument(definition.FindOperation(operation)!, request.Input), Json, stopping).ConfigureAwait(false);
            RefreshViews(instance, presenter);
            await PersistStateAsync(app, instance).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        { AppLog.Log($"{definition.Id}.{operation} failed for request {request.RequestId}: {error}"); }
    }

    private static void RefreshViews(App instance, IPresenter presenter)
    {
        var type = instance.GetType();
        var side = ReadProperty(type, instance, "Side") ?? InvokeRenderMethod(type, instance, "RenderSide", "Side");
        var main = ReadProperty(type, instance, "Main") ?? InvokeRenderMethod(type, instance, "Render", "Main");
        if (side is not null || main is not null)
        {
            presenter.Present(side, main);
        }
    }

    private static View? ReadProperty(Type type, App instance, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (property is null || !(typeof(View).IsAssignableFrom(property.PropertyType) || typeof(Node).IsAssignableFrom(property.PropertyType)))
        {
            return null;
        }
        var value = property.GetValue(instance);
        return value as View ?? (value is Node node ? new View(name, node) : null);
    }

    private static View? InvokeRenderMethod(Type type, App instance, string methodName, string fallbackTitle)
    {
        var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes);
        if (method is null || !(typeof(View).IsAssignableFrom(method.ReturnType) || typeof(Node).IsAssignableFrom(method.ReturnType)))
        {
            return null;
        }
        var value = method.Invoke(instance, null);
        return value as View ?? (value is Node node ? new View(fallbackTitle, node) : null);
    }

    private static object? Argument(MethodInfo method, string input)
    {
        var parameter = method.GetParameters().FirstOrDefault(p => p.ParameterType != typeof(CancellationToken));
        if (parameter is null || parameter.ParameterType == typeof(string)) { return string.IsNullOrEmpty(input) ? null : input; }
        return string.IsNullOrWhiteSpace(input) ? null : JsonSerializer.Deserialize(input, parameter.ParameterType, Json);
    }

    // The code is the operation list: every [On] operation is served. Manifest names keep their spelling, and an
    // operation the manifest omits is served under its kebab-case method name. Signal reactions are not requests.
    internal static string[] ServedOperations(AppDefinition definition, IEnumerable<string> manifestNames)
    {
        var reactions = definition.SignalSubscriptions.Select(sub => sub.Method).ToHashSet();
        var served = new List<string>();
        var covered = new HashSet<MethodInfo>();
        foreach (var name in manifestNames)
        {
            if (definition.FindOperation(name) is { } method && covered.Add(method)) { served.Add(name); }
        }
        foreach (var method in definition.Operations.Values.OrderBy(method => method.Name, StringComparer.Ordinal))
        {
            if (!reactions.Contains(method) && covered.Add(method)) { served.Add(Kebab(method.Name)); }
        }
        return [.. served];
    }

    internal static string Kebab(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]) ? "-" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    // Returns the JSON it restored. Any failure throws: the runner faults instead of starting with a default state.
    internal static string? RestoreState(App instance, string? stateJson)
    {
        if (string.IsNullOrWhiteSpace(stateJson)) { return null; }
        var type = instance.GetType();
        var stateProp = type.GetProperty("State", BindingFlags.Public | BindingFlags.Instance);
        if (stateProp is null) { return null; }
        object? restored;
        try { restored = JsonSerializer.Deserialize(stateJson, stateProp.PropertyType, Json); }
        catch (JsonException error) { throw new AppStateRestoreException($"The saved state does not read as {stateProp.PropertyType.Name}: {error.Message}", error); }
        if (restored is null) { throw new AppStateRestoreException($"The saved state reads as null for {stateProp.PropertyType.Name}."); }
        var setMethod = type.GetMethod("Set", BindingFlags.NonPublic | BindingFlags.Instance, [stateProp.PropertyType])
            ?? throw new AppStateRestoreException($"{type.Name} has no Set({stateProp.PropertyType.Name}) to restore its state.");
        setMethod.Invoke(instance, [restored]);
        return stateJson;
    }

    private async Task PersistStateAsync(IApp app, App instance)
    {
        try
        {
            var type = instance.GetType();
            var stateProp = type.GetProperty("State", BindingFlags.Public | BindingFlags.Instance);
            if (stateProp is null) { return; }
            var current = stateProp.GetValue(instance);
            if (current is null) { return; }
            var json = JsonSerializer.Serialize(current, Json);
            if (json == _lastSavedState) { return; }
            _lastSavedState = json;
            var saveMethod = app.GetType().GetMethod("SaveRuntimeState", [typeof(string), typeof(long)]);
            if (saveMethod is not null)
            {
                var saveTask = (Task)saveMethod.Invoke(app, [json, _stateRevision])!;
                await saveTask.ConfigureAwait(false);
                var resultProp = saveTask.GetType().GetProperty("Result");
                if (resultProp?.GetValue(saveTask) is { } updated && updated.GetType().GetProperty("RuntimeStateRevision")?.GetValue(updated) is long newRev)
                {
                    // The app neuron ignores a save behind its revision: another writer saved first, so this state was not kept.
                    if (newRev != _stateRevision + 1)
                    {
                        _lastSavedState = null;
                        AppLog.Log($"State save at revision {_stateRevision} was not kept; the app holds revision {newRev}. Another runner may be writing this app.");
                    }
                    _stateRevision = newRev;
                }
            }
        }
        catch (Exception error)
        {
            AppLog.Log($"Failed to persist app state: {error.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        var context = Interlocked.Exchange(ref _context, null);
        if (context is null) { return; }
        try
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
            await Task.WhenAny(Completion, Task.Delay(StopGrace)).ConfigureAwait(false);
        }
        finally
        {
            context.Unload();
            context = null;
            _stopping.Dispose();
        }
    }

    internal sealed class AppLoadContext(string behavior) : AssemblyLoadContext("app " + behavior, isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name)
        {
            if (Default.Assemblies.FirstOrDefault(loaded => AssemblyName.ReferenceMatchesDefinition(name, loaded.GetName())) is { } shared) { return shared; }
            var candidate = Path.Combine(System.AppContext.BaseDirectory, name.Name + ".dll");
            return File.Exists(candidate) ? Default.LoadFromAssemblyPath(candidate) : null;
        }
    }
}

// The saved state of an app cannot be restored into its current TState.
public sealed class AppStateRestoreException(string message, Exception? inner = null) : InvalidOperationException(message, inner);
