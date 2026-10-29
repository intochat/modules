using System.Reflection;
using System.Text.Json;
using DigitalBrain;
using DigitalBrain.UI;
using IntoChat.OS.Client;
using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Modules.Apps.Signals;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

// The generic entry of a program that is a framework [App] class instead of a script: it builds the app from the framework's own
// AppDefinition (the rules TestOS applies) and maps each brain.On(app, operation) request onto the matching [On] method, one request at a
// time, as turns. What the app presents goes to the app's view neurons through the client. A program is an [App] class when its assembly has
// no entry point and declares exactly one.
internal static class AppRunner
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Type? AppTypeIn(Assembly assembly)
    {
        var apps = assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false } && type.IsDefined(typeof(AppAttribute), false)).ToArray();
        return apps.Length == 1 ? apps[0] : null;
    }

    public static async Task<int> RunAsync(Type appType, CancellationToken stopping)
    {
        await using var brain = await IntoChatOSClient.ConnectAsync([], stopping).ConfigureAwait(false);
        var appKey = brain.Setting("App") ?? throw new InvalidOperationException("An [App] program is assigned with the Setting \"App\": its app key.");
        var definition = new AppDefinition(appType);
        var presenter = new ViewPresenter(brain, appKey, long.Parse(brain.Setting("ProgramRevision") ?? "1", System.Globalization.CultureInfo.InvariantCulture));
        var instance = definition.Create(service => service == typeof(IPresenter) ? presenter : null);
        instance.AttachNeuronResolver((contractType, key) => ResolveNeuron(brain, contractType, key));
        var app = brain.Get<IApp>(appKey);
        // The operations the app is requested by are the manifest's; each one that is an [On] method is served,
        // alongside any background neuron [Signal] subscriptions declared via [On("sourceKey")].
        var snapshot = await app.Read().ConfigureAwait(false);
        var turns = new SemaphoreSlim(1, 1);
        var operationLoops = snapshot.Manifest.Operations.Where(operation => definition.FindOperation(operation.Name) is not null)
            .Select(operation => Serve(brain, app, definition, instance, presenter, operation.Name, turns, stopping));
        var signalLoops = definition.SignalSubscriptions
            .Select(sub => ServeSignalSubscription(brain, definition, instance, presenter, sub, turns, stopping));
        var loops = operationLoops.Concat(signalLoops).ToArray();
        try { await Task.WhenAll(loops).ConfigureAwait(false); }
        finally { await presenter.DrainAsync().ConfigureAwait(false); }
        return 0;
    }

    private static object ResolveNeuron(IntoChatOSConnection brain, Type contractType, string key)
        => typeof(IntoChatOSConnection)
            .GetMethod(key.Contains('/') ? nameof(IntoChatOSConnection.Get) : nameof(IntoChatOSConnection.Neuron), [typeof(string)])!
            .MakeGenericMethod(contractType)
            .Invoke(brain, [key])!;

    private static Task ServeSignalSubscription(
        IntoChatOSConnection brain,
        AppDefinition definition,
        App instance,
        IPresenter presenter,
        AppSignalSubscription sub,
        SemaphoreSlim turns,
        CancellationToken stopping)
        => (Task)typeof(AppRunner)
            .GetMethod(nameof(ServeSignalGeneric), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(sub.SignalType)
            .Invoke(null, [brain, definition, instance, presenter, sub, turns, stopping])!;

    private static async Task ServeSignalGeneric<TSignal>(
        IntoChatOSConnection brain,
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
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    AppLog.Log($"{definition.Manifest.Id}.{sub.OperationName} failed for signal {typeof(TSignal).Name}: {error}");
                }
                finally
                {
                    turns.Release();
                }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
    }

    private static async Task Serve(IntoChatOSConnection brain, IApp app, AppDefinition definition, App instance, IPresenter presenter, string operation, SemaphoreSlim turns, CancellationToken stopping)
    {
        try
        {
            await foreach (var request in brain.On(app, operation, stopping).ConfigureAwait(false))
            {
                await turns.WaitAsync(stopping).ConfigureAwait(false);
                try { await Turn(definition, instance, presenter, operation, request, stopping).ConfigureAwait(false); }
                finally { turns.Release(); }
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
    }

    // An operation that throws has failed its request, not the app: the failure goes to the run log, and the next request is served.
    private static async Task Turn(AppDefinition definition, App instance, IPresenter presenter, string operation, AppRequested request, CancellationToken stopping)
    {
        try
        {
            await definition.Invoke(instance, operation, Argument(definition.FindOperation(operation)!, request.Input), Json, stopping).ConfigureAwait(false);
            RefreshViews(instance, presenter);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        { AppLog.Log($"{definition.Manifest.Id}.{operation} failed for request {request.RequestId}: {error}"); }
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

    // A request's input is text. An operation that takes a string gets it as is; one that takes a signal or a value gets it read as JSON,
    // an empty input being the empty value, as TestOS requests an operation without an argument.
    private static object? Argument(MethodInfo method, string input)
    {
        var parameter = method.GetParameters().FirstOrDefault(p => p.ParameterType != typeof(CancellationToken));
        if (parameter is null || parameter.ParameterType == typeof(string)) { return string.IsNullOrEmpty(input) ? null : input; }
        return string.IsNullOrWhiteSpace(input) ? null : JsonSerializer.Deserialize(input, parameter.ParameterType, Json);
    }
}
