using System.Net.ServerSentEvents;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using IntoChat.OS.Client.Runtime;
using IntoChat.OS.Client.Runtime.V1;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Connections.Contracts;
using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;
using IntoChat.OS.Modules.Microsoft.CSharp.Sessions;
using Microsoft.AspNetCore.Http;
using DigitalBrain;
using DigitalBrain.Access;
using DigitalBrain.Runtime.Orleans.Hosting;
using Orleans;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Edge;

// Everything a sandbox session does to its brain passes here: the token must speak for this brain and
// for the session its runtime currently records, only neuron contracts from installed contract
// assemblies are callable, and each call carries the behavior owner's caller context re-stamped as an
// app behind the app proxy.
internal sealed class RuntimeEdge(SessionTokens tokens, CompiledTypes types, IGrainFactory grains, SessionStreams streams, IKernel kernel, RuntimeDirectory directory,
    IServiceProvider? plain = null)
{
    public async Task<(SessionClaims Claims, IBrainRuntimeEdge Runtime)> AuthorizeAsync(string brainId, string? token)
    {
        var claims = ValidateToken(brainId, token);
        var runtime = Runtime(brainId);
        return (await runtime.Authorize(claims.Session).ConfigureAwait(false)).Active
            ? (claims, runtime)
            : throw new UnauthorizedAccessException("This session no longer speaks for its brain.");
    }

    public async Task<JsonElement?> InvokeAsync(string brainId, string? token, RuntimeInvocation invocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var claims = ValidateToken(brainId, token);
        var identity = await IdentityOf(brainId, claims.Session, invocation.Behavior).ConfigureAwait(false);
        if (invocation.Contract == RuntimeProtocol.KernelContract) { return await AskKernelAsync(brainId, identity, invocation, cancellationToken).ConfigureAwait(false); }
        var contract = Callable(invocation.Contract);
        var method = contract.GetMethods().Concat(contract.GetInterfaces().SelectMany(inherited => inherited.GetMethods()))
            .Where(candidate => candidate.Name == invocation.Method)
            .SingleOrDefault(candidate => candidate.GetParameters().Count(IsArgument) == invocation.Arguments.Length)
            ?? throw new ArgumentException($"{contract.Name} has no method {invocation.Method} taking {invocation.Arguments.Length} arguments.");
        RequireTargetBrain(brainId, invocation.Key, invocation.Method, identity,
            method.GetCustomAttribute<AccessAttribute>() ?? contract.GetCustomAttribute<AccessAttribute>());
        var parameters = method.GetParameters();
        var values = new object?[parameters.Length];
        for (int index = 0, argument = 0; index < parameters.Length; index++)
        {
            values[index] = IsArgument(parameters[index])
                ? invocation.Arguments[argument++].Deserialize(parameters[index].ParameterType, RuntimeProtocol.Json)
                : cancellationToken;
        }
        // Transitional (06-07): a plain contract (06-03 seam) is called on its runtime host after the same access checks.
        var neuron = IntoChat.OS.Runtime.PlainNeuronContracts.Is(contract) ? SiloNeurons.Get(Plain, contract, invocation.Key) : grains.GetGrain(contract, invocation.Key);
        Stamp(identity);
        object? returned;
        try { returned = method.Invoke(neuron, values); }
        catch (TargetInvocationException error) when (error.InnerException is not null) { throw error.InnerException; }
        if (returned is not Task task) { throw new NotSupportedException($"{contract.Name}.{method.Name} does not return a Task."); }
        var result = await TaskBoxing.ResultAsync(task, method.ReturnType).ConfigureAwait(false);
        if (!method.ReturnType.IsGenericType) { return null; }
        return JsonSerializer.SerializeToElement(result, method.ReturnType.GetGenericArguments()[0], RuntimeProtocol.Json);
    }

    // The kernel answers as the behavior's owner, and only about the brain the session speaks for.
    private async Task<JsonElement?> AskKernelAsync(string brainId, BehaviorIdentity identity, RuntimeInvocation invocation, CancellationToken cancellationToken)
    {
        if (identity.Principal is null) { throw new UnauthorizedAccessException("A behavior without an owner cannot ask the kernel."); }
        Stamp(identity);
        if (BrainScope.CurrentId() != brainId) { throw new UnauthorizedAccessException("The kernel answers only about the session's own brain."); }
        string? Argument(int index) => invocation.Arguments.Length > index ? invocation.Arguments[index].Deserialize<string?>(RuntimeProtocol.Json) : null;
        object answer;
        switch (invocation.Method)
        {
            case RuntimeProtocol.KernelTypes when invocation.Arguments.Length == 0:
                answer = await kernel.Types(cancellationToken).ConfigureAwait(false);
                break;
            case RuntimeProtocol.KernelInstances when invocation.Arguments.Length == 2:
                var type = Argument(0) is { } contract ? kernel.TypeOf(Callable(contract)) : Argument(1);
                List<BrainInstance> listed = [];
                await foreach (var instance in kernel.Instances(type, cancellationToken).ConfigureAwait(false)) { listed.Add(instance); }
                answer = listed.ToArray();
                break;
            case RuntimeProtocol.KernelRunning when invocation.Arguments.Length == 0:
                answer = await kernel.Running(cancellationToken).ConfigureAwait(false);
                break;
            case RuntimeProtocol.KernelBrains when invocation.Arguments.Length == 0:
                answer = await kernel.Brains(cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentException($"The kernel has no method {invocation.Method} taking {invocation.Arguments.Length} arguments.");
        }
        return JsonSerializer.SerializeToElement(answer, RuntimeProtocol.Json);
    }

    public async Task<IResult> ReportAsync(string brainId, string? token, RuntimeReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var claims = ValidateToken(brainId, token);
        var runtime = Runtime(brainId);
        // Each runtime mutation validates the session in the same turn as its write.
        // A separate preflight doubles the calls and cannot authorize a later turn.
        var members = (report.Fingerprint is null ? 0 : 1) + (report.Subscribe is null ? 0 : 1)
            + (report.Acknowledge is null ? 0 : 1) + (report.Acknowledgements is null ? 0 : 1)
            + (report.Status is null ? 0 : 1) + (report.Logs is null ? 0 : 1) + (report.Heartbeat ? 1 : 0);
        if (members != 1) { return Results.BadRequest("A report carries exactly one member."); }
        if (report.Fingerprint is { } fingerprint)
        {
            await runtime.Connected(claims.Session, fingerprint).ConfigureAwait(false);
            return ContractsFingerprint.Covers(fingerprint, ContractsFingerprint.Of(types.Assemblies))
                ? Results.Ok()
                : Results.Conflict("The sandbox image carries other contracts than this brain; rebuild it from the same build.");
        }
        if (report.Subscribe is { } subscribe)
        {
            // Registered before the first batch is sent, so a refusal surfaces here and not on the stream.
            var contract = Callable(subscribe.Contract);
            // A neuron is named by its runtime id; the runtime hears it through the session synapse neuron.
            var neuron = IntoChat.OS.Runtime.PlainNeuronContracts.Is(contract)
                ? PlainSources.Of(SiloNeurons.IdOf(contract, subscribe.Key))
                : throw new ArgumentException($"{subscribe.Contract} is not a neuron contract.");
            var identity = await IdentityOf(brainId, claims.Session, subscribe.Behavior).ConfigureAwait(false);
            RequireTargetBrain(brainId, subscribe.Key, "Watch", identity, contract.GetCustomAttribute<AccessAttribute>());
            var id = await runtime.Subscribed(claims.Session, subscribe.Behavior, neuron, subscribe.Signal, subscribe.Selector).ConfigureAwait(false);
            return Results.Ok(new { subscription = id });
        }
        if (report.Acknowledge is { } acknowledge) { await runtime.Acknowledge(claims.Session, acknowledge.Subscription, acknowledge.Watermark).ConfigureAwait(false); return Results.Ok(); }
        if (report.Acknowledgements is { } acknowledgements) { await runtime.AcknowledgeMany(claims.Session, acknowledgements).ConfigureAwait(false); return Results.Ok(); }
        if (report.Status is { } status) { await runtime.Reported(claims.Session, status).ConfigureAwait(false); return Results.Ok(); }
        if (report.Logs is { } logs)
        {
            // Logs complete locally rather than on the runtime's guarded mutation turn.
            if (!(await runtime.Authorize(claims.Session).ConfigureAwait(false)).Active)
            { throw new UnauthorizedAccessException("This session no longer speaks for its brain."); }
            streams.CompleteLogs(logs.Request, logs.Text);
            return Results.Ok();
        }
        if (report.Heartbeat) { await runtime.Heartbeat(claims.Session).ConfigureAwait(false); return Results.Ok(); }
        return Results.BadRequest("A report carries exactly one member.");
    }

    // Authorizes before the first byte, so a refused token is a 401 and not an empty stream. The
    // channel opens before assignments and pending batches are read, so nothing pushed meanwhile is lost;
    // a batch sent twice is deduplicated by its watermark on the host.
    public async Task<IAsyncEnumerable<SseItem<string>>> OpenStreamAsync(string brainId, string? token, CancellationToken cancellationToken)
    {
        var (claims, runtime) = await AuthorizeAsync(brainId, token).ConfigureAwait(false);
        var channel = streams.Open(claims.Session);
        var assignments = await runtime.Assignments(claims.Session).ConfigureAwait(false);
        var pending = await runtime.PendingBatches(claims.Session).ConfigureAwait(false);
        return Streaming(claims.Session, tokens.Issue(claims.Brain, claims.Session), channel, assignments, pending, cancellationToken);
    }

    // Ready names the whole desired set, then a fresh token: a host reconnecting after a missed rotation
    // keeps speaking, and one that missed a retirement unloads it.
    private async IAsyncEnumerable<SseItem<string>> Streaming(string session, string token, System.Threading.Channels.Channel<(string Kind, object Payload)> channel,
        IReadOnlyList<AssignmentEvent> assignments, IReadOnlyList<SignalBatchEvent> pending, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            yield return Item(ReadyEvent.Kind, new ReadyEvent(session, [.. assignments.Select(assignment => assignment.Behavior)]));
            yield return Item(TokenEvent.Kind, new TokenEvent(token));
            foreach (var assignment in assignments) { yield return Item(AssignmentEvent.Kind, assignment); }
            foreach (var batch in pending) { yield return Item(SignalBatchEvent.Kind, batch); }
            await foreach (var (kind, payload) in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) { yield return Item(kind, payload); }
        }
        finally { streams.Close(session, channel); }
    }

    private IServiceProvider Plain => plain ?? throw new NotSupportedException("This host does not reach plain neurons: the kernel composition registers them.");

    private Type Callable(string name)
        // Only neuron contracts are kernel types; kernel grains and inbound plumbing are not neurons.
        => types.Contract(name);

    // The directory answers once this process has published the session. Anything it has not seen is the grain's.
    private async Task<BehaviorIdentity> IdentityOf(string brainId, string session, string behavior)
    {
        var key = BrainRuntimeKeys.For(brainId);
        if (!directory.TryIdentity(key, session, behavior, out var identity))
        { identity = await Runtime(brainId).Identity(session, behavior).ConfigureAwait(false); }
        return identity ?? throw new ArgumentException($"{behavior} is not a behavior of this brain.");
    }

    // The brain's runtime is a plain neuron (06-08), reached through the host seam.
    private IBrainRuntimeEdge Runtime(string brainId)
        => SiloNeurons.Get<IBrainRuntimeEdge>(Plain, BrainRuntimeKeys.For(brainId));

    private SessionClaims ValidateToken(string brainId, string? token)
    {
        var claims = tokens.Validate(token) ?? throw new UnauthorizedAccessException("The session token is missing, invalid or expired.");
        if (claims.Brain != brainId) { throw new UnauthorizedAccessException("The session token speaks for another brain."); }
        return claims;
    }

    private static void RequireTargetBrain(string brainId, string key, string operation, BehaviorIdentity identity, AccessAttribute? access)
    {
        // Recorded callers use the normal neuron policy, including explicitly bound connections.
        // A platform-assigned behavior has no caller; its session still cannot reach another brain.
        if (identity.Caller is not null || BrainKey.BelongsTo(key, brainId)) { return; }
        if (BrainKey.ScopeOf(key) is not null || access is not (PublicAttribute or SharedAttribute))
        { throw new UnauthorizedAccessException($"The session cannot address a neuron in another brain (operation {operation})."); }
    }

    // A behavior started without a caller (tests, the platform) calls without a stamp, as grain code does.
    private static void Stamp(BehaviorIdentity identity)
    {
        if (identity.Caller is { } caller) { CallerContextStamper.Stamp(caller); }
    }

    private static SseItem<string> Item(string kind, object payload) => new(JsonSerializer.Serialize(payload, payload.GetType(), RuntimeProtocol.Json), kind);

    private static bool IsArgument(ParameterInfo parameter) => parameter.ParameterType != typeof(CancellationToken);
}
