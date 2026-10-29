using System.Collections.Concurrent;
using System.Runtime.Loader;
using IntoChat.OS.Client.Runtime;
using IntoChat.OS.Client.Runtime.V1;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

// The sessions this process holds: many locally (one container serves every brain), one in a pool
// session (the pool routes by identifier). A start is accepted at once and the session dials the brain
// on its own: the brain starts sessions from inside a grain turn, and answering only after dialing back
// would wait on that very turn. A start with a new session id replaces the old one, whose token no
// longer speaks for the brain. Ensuring a held session refreshes its token so a restarted brain can
// restore credentials without replacing behaviors. A session that cannot connect is forgotten, so
// the brain's next check finds it gone and starts it again.
public sealed class Sessions(BehaviorLoader loader, ILoggerFactory loggers, HttpMessageHandler? edge = null)
{
    private readonly ConcurrentDictionary<string, Open> _open = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger _logger = loggers.CreateLogger<Sessions>();

    // The contracts this image carries, reported to every brain on connect.
    public static string Fingerprint { get; } = ResolveFromBaseDirectory() ?? ContractsFingerprint.Of(Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll")
        .Where(path => Path.GetFileNameWithoutExtension(path) is "IntoChat.OS" or "IntoChat.OS.Client" or "DigitalBrain" or "DigitalBrain.UI"
            || Path.GetFileNameWithoutExtension(path).EndsWith(".Contracts", StringComparison.Ordinal)
            || (Path.GetFileName(path).StartsWith("IntoChat.OS.Modules.", StringComparison.Ordinal) && ContractsHost.Declares(path)))
        .Select(path => AssemblyLoadContext.Default.Assemblies.FirstOrDefault(loaded => loaded.GetName().Name == Path.GetFileNameWithoutExtension(path))
            ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(path)));

    // Contracts copied beside the host may reference each other (Mcp.Contracts -> Tools.Contracts) or the core
    // packages (DigitalBrain, DigitalBrain.UI) without being in its deps.json, so the default context resolves them from the host's directory. Installed
    // before the fingerprint loads every contract; returns null so the fingerprint is computed after it.
    private static string? ResolveFromBaseDirectory()
    {
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var candidate = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };
        return null;
    }

    public async Task StartAsync(string identifier, SessionStart start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        var edgeUrl = new Uri(start.EdgeUrl);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_open.TryGetValue(identifier, out var current) && current.Id == start.Session)
            { current.RefreshToken(start.Token); return; }
            await StopCoreAsync(identifier).ConfigureAwait(false);
            var open = new Open(start.Session, start.Token);
            _open[identifier] = open;
            open.Running = RunAsync(identifier, open, start, edgeUrl);
        }
        finally { _gate.Release(); }
    }

    public bool Contains(string identifier) => _open.ContainsKey(identifier);

    public async Task<bool> StopAsync(string identifier)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { return await StopCoreAsync(identifier).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task RunAsync(string identifier, Open open, SessionStart start, Uri edgeUrl)
    {
        await Task.Yield();
        // A session outlives the request that started it; its work belongs to the traces of the signals it handles.
        System.Diagnostics.Activity.Current = null;
        try
        {
            SessionConnection connection;
            while (true)
            {
                var token = open.Token;
                try
                {
                    connection = await SessionConnection.OpenAsync(edgeUrl, token, Fingerprint, edge, open.Lifetime.Token).ConfigureAwait(false);
                    break;
                }
                // An ensure can refresh credentials while the original handshake is in flight.
                catch (Exception error) when (error is not OperationCanceledException && token != open.Token) { _logger.LogDebug(error, "Session handshake superseded by refreshed credentials; retrying"); }
            }
            var session = new Session(connection, loader, loggers.CreateLogger("Session " + start.Brain));
            open.Attach(session);
            _logger.LogInformation("Session {Session} of brain {Brain} connected", start.Session, start.Brain);
            await session.RunAsync(open.Lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (open.Lifetime.IsCancellationRequested) { }
        catch (Exception error)
        {
            _logger.LogError(error, "Session {Session} of brain {Brain} could not connect; the brain starts it again", start.Session, start.Brain);
            _open.TryRemove(new(identifier, open));
        }
    }

    private async Task<bool> StopCoreAsync(string identifier)
    {
        if (!_open.TryRemove(identifier, out var open)) { return false; }
        await open.Lifetime.CancelAsync().ConfigureAwait(false);
        await open.Running.ConfigureAwait(false);
        if (open.Session is { } session) { await session.DisposeAsync().ConfigureAwait(false); }
        open.Lifetime.Dispose();
        return true;
    }

    private sealed class Open(string id, string token)
    {
        private readonly object gate = new();
        private string latestToken = token;
        public string Id { get; } = id;
        public string Token { get { lock (gate) { return latestToken; } } }
        public CancellationTokenSource Lifetime { get; } = new();
        public Task Running { get; set; } = Task.CompletedTask;
        public Session? Session { get; private set; }
        public void RefreshToken(string value)
        {
            lock (gate) { latestToken = value; Session?.RefreshToken(value); }
        }
        public void Attach(Session session)
        {
            lock (gate) { session.RefreshToken(latestToken); Session = session; }
        }
    }
}
