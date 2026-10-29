using Microsoft.Extensions.Logging;
using IntoChat.OS.Framework.Diagnostics;
using DigitalBrain;
using System.Text;
using System.Text.Json;
using IntoChat.OS.Modules.AI.Models;
using IntoChat.OS.Modules.AI.Routing.Signals;
using IntoChat.OS.Modules.AI.SystemOne;
using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Connections.Contracts;
using IntoChat.OS.Connections.Surface;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using DigitalBrain.Runtime.Orleans.Hosting;

namespace IntoChat.OS.Modules.AI.Routing;

// The Jev router. It reads the deployed contract catalog, asks a System One model which contract
// and method fit the request, gates on confidence, then fills the call and invokes it. The catalog
// is the menu; confidence is the trust knob; a language model only fills key and arguments.
// It keeps its own decisions and shows them to the model, so each request is judged against state.
[Neuron("ai.router", State = "router")]
internal sealed class RouterNeuron : AiNeuron<RouterStorage>, IRouter, IRouterWork
{
    private const int MaxOptions = 255;
    private const int MaxRecent = 16;
    private const int MaxAsked = 64;
    private const int MaxSteps = 8;
    private const int MaxRequestChars = 2000;
    private const double Gate = 0.5;
    private const string NotClear = "The request was not clear enough to route.";
    private const string NeedsInput = "The operation needs input filled from the request, so it was not carried out directly.";
    // Never routed to: the legacy observer path (Watch/Unwatch, until 06-08 deletes it) and a plain neuron's admission hook
    // (AdmitSynapse, R4-03), which only the runtime calls before it creates a synapse (T-06-14).
    internal static readonly HashSet<string> NotCallable = ["Watch", "Unwatch", "AdmitSynapse"];
    private string Key => FullKey;
    private string Scope => Id.Brain.Value;
    private NeuronTypeSearch Capabilities() => ServiceProvider.GetRequiredService<NeuronTypeSearch>();

    // An app of this brain by its os key (apps are plain since 06-08).
    private IApp App(string appKey)
        => Get<IApp>(new DigitalBrain.NeuronId(Id.Brain, IntoChat.OS.Connections.AppGrainType.Name, appKey[(Scope.Length + 1)..]));

    public async Task Ask(RouteRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.State);
        var asked = Snapshot.Asked.Where(item => item.Id != request.Id).TakeLast(MaxAsked - 1);
        await Save(Snapshot with { Asked = [.. asked, new PendingRoute(request.Id, Clip(request.State), "Routing", request.Allowed)] }, new RouterDecided(Key, Snapshot.Recent.Length));
        var token = "route:" + request.Id.ToString("N");
        var gate = ServiceProvider.GetService<IWorkContinuation>();
        try
        {
            if (gate is not null) { await gate.Acquire(Scope, token); }
            await Context.Get<IRouterWork>(Id).Decide(request.Id);
        }
        catch (Exception error)
        {
            try
            {
                if (gate is not null) { await gate.Release(Scope, token); }
                var remaining = Snapshot.Asked.Where(item => item.Id != request.Id).ToArray();
                await Save(Snapshot with { Asked = remaining }, new RouterDecided(Key, Snapshot.Recent.Length));
            }
            catch (Exception rollback)
            { throw new InvalidOperationException(error.Message, rollback); }
            throw;
        }
    }

    public Task<PendingRoute?> Pending(Guid id, CancellationToken ct) => Task.FromResult(Snapshot.Asked.FirstOrDefault(item => item.Id == id));

    // A one-way turn: the model takes as long as it takes and no caller waits on it, so the System One
    // HTTP call is made here directly rather than through a grain call bounded by the response timeout.
    public async Task Decide(Guid id)
    {
        try
        {
            if (Snapshot.Asked.FirstOrDefault(item => item.Id == id) is not { Status: "Routing" } asked) { return; }
            var http = ServiceProvider.GetRequiredService<HttpSystemOne>();
            PendingRoute settled;
            Routed routed;
            try
            {
                var result = await Decide(asked.State, asked.Allowed, request => http.Ask(request, CancellationToken.None), CancellationToken.None);
                // No route fits: the request is decided with no result (a result is a route, and its hint names it)
                // and the reason, so a reader never shows an empty route as a likely capability.
                (settled, routed) = result.Contract.Length > 0
                    ? (asked with { Status = "Routed", Result = result }, new Routed(id, result.Contract, result.Method, result.Confidence))
                    : (asked with { Status = "Routed", Error = NotClear }, new Routed(id, "", "", 0, NotClear));
            }
            catch (Exception error)
            {
                settled = asked with { Status = "Failed", Error = error.Message };
                routed = new Routed(id, "", "", 0, error.Message);
            }
            var recent = settled.Result is { } route ? Recorded(asked.State, route.Contract, route.Method) : Snapshot.Recent;
            await Save(Snapshot with { Recent = recent, Asked = [.. Snapshot.Asked.Select(item => item.Id == id ? settled : item)] },
                new RouterDecided(Key, recent.Length), routed);
        }
        finally
        {
            if (ServiceProvider.GetService<IWorkContinuation>() is { } gate) { await gate.Release(Scope, "route:" + id.ToString("N")); }
        }
    }

    public async Task<RouteResult> Route(string state, string[]? allowed, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        var systemone = SiloNeurons.Get<ISystemOne>(ServiceProvider, ISystemOne.Key(Scope));
        var result = await Decide(state, allowed, request => systemone.Ask(request, ct), ct);
        if (result.Contract.Length > 0) { await Remember(state, result.Contract, result.Method); }
        return result;
    }

    private async Task<RouteResult> Decide(string state, string[]? allowed, Func<SystemOneRequest, Task<SystemOneResult>> ask, CancellationToken ct)
    {
        if (AppsAllowed(allowed) && await AppTier(state, ask) is { } app) { return app; }
        var types = await Menu(allowed, ct);
        if (types.Length == 0) { return Unclear(); }

        var moment = AwareOfState(state);
        var criteria = types.ToDictionary(type => type.Id, type => (string?)Trim(type.Description), StringComparer.Ordinal);
        var contractQuestions = new Dictionary<string, SystemOneQuestion>(StringComparer.Ordinal);
        var chunks = Chunk(criteria).ToArray();
        for (var index = 0; index < chunks.Length; index++)
        { contractQuestions["capability_" + index] = new SystemOneQuestion.Choice("Which capability can carry out this request?", chunks[index]); }
        var contractAnswer = await ask(new SystemOneRequest(moment, contractQuestions));

        if (Best(contractAnswer, criteria.Keys) is not { } pick) { return Unclear(); }
        var (chosenId, confidence, probabilities) = pick;
        // An id outside the menu is the same unclear answer as no pick at all.
        if (types.FirstOrDefault(type => type.Id == chosenId) is not { } chosen) { return Unclear(); }
        if (chosen.Key is not null)
        {
            return new RouteResult(chosen.Contract, "Run", confidence, probabilities, chosen.Key, chosen.Tool);
        }
        var methods = chosen.Methods.Select(MethodName).Where(name => !NotCallable.Contains(name)).Distinct(StringComparer.Ordinal).ToArray();
        if (methods.Length == 0)
        {
            return new RouteResult(chosen.Contract, "", confidence, probabilities);
        }

        var methodAnswer = await ask(new SystemOneRequest(moment,
            new() { ["method"] = new SystemOneQuestion.Choice("Which method carries out this request?", methods.ToDictionary(name => name, _ => (string?)null, StringComparer.Ordinal)) }));
        var method = Best(methodAnswer, methods)?.Id ?? methods[0];
        return new RouteResult(chosen.Contract, method, confidence, probabilities);
    }

    // The first of two tiers: the brain's installed apps' operations, except the asking app's own (a request an
    // app sends is its own to answer, so the assistant's "hello" never routes back to assistant.open). A
    // "something else" answer, or one naming no offered operation, falls through to the second tier. An app
    // route carries the app and operation, so Dispatch has no contract to fill and no language model runs.
    private async Task<RouteResult?> AppTier(string state, Func<SystemOneRequest, Task<SystemOneResult>> ask)
    {
        var asker = Asker();
        var criteria = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var operation in await MenuOperations())
        {
            if (operation.App == asker) { continue; }
            criteria[operation.App + "." + operation.Name] = string.IsNullOrWhiteSpace(operation.Description) ? null : Trim(operation.Description);
        }
        if (criteria.Count == 0) { return null; }
        criteria["something else"] = "The request is not about an installed app.";
        var moment = AwareOfState(state);
        var questions = new Dictionary<string, SystemOneQuestion>(StringComparer.Ordinal);
        var chunks = Chunk(criteria).ToArray();
        for (var index = 0; index < chunks.Length; index++)
        { questions["app_" + index] = new SystemOneQuestion.Choice("Which installed app operation can carry out this request?", chunks[index]); }
        var answer = await ask(new SystemOneRequest(moment, questions));
        if (Best(answer, criteria.Keys) is not { } pick || pick.Id == "something else") { return null; }
        var (chosen, confidence, probabilities) = pick;
        var split = chosen.IndexOf('.');
        if (split <= 0 || split >= chosen.Length - 1) { return null; }
        return new RouteResult(typeof(IApp).FullName!, nameof(IApp.Request), confidence, probabilities,
            App: chosen[..split], Operation: chosen[(split + 1)..]);
    }

    // Reads every installed app only until the menu exists. Later changes arrive through AppMenuHost.
    private async Task<MenuOperation[]> MenuOperations()
    {
        var catalog = ServiceProvider.GetRequiredService<AppOperationCatalog>();
        if (catalog.TryGet(Scope, out var cached) && await Fresh(catalog)) { return catalog.TryGet(Scope, out var current) ? current : cached; }
        List<(string App, MenuOperation[] Operations)> loaded = [];
        try
        {
            var kernel = ServiceProvider.GetRequiredService<IKernel>();
            await foreach (var instance in kernel.InstancesIn(Scope, kernel.TypeOf(typeof(IApp))))
            {
                if (IApp.NameOf(Scope, instance.Key) is not { } name) { continue; }
                AppSnapshot snapshot;
                try { snapshot = await App(instance.Key).Read(); }
                catch (Exception error) when (error is not OperationCanceledException) { return []; }
                if (snapshot.MigrationTarget is not null || snapshot.Manifest is null) { continue; }
                loaded.Add((name, snapshot.Manifest.Operations
                    .Select(operation => new MenuOperation(name, operation.Name, string.IsNullOrWhiteSpace(operation.Description) ? null : operation.Description))
                    .ToArray()));
            }
        }
        catch (Exception error) when (error is not OperationCanceledException) { return []; }
        catalog.Replace(Scope, loaded);
        return loaded.SelectMany(item => item.Operations).ToArray();
    }

    // Reads into the ready menu the installed apps it has not read yet; false when the directory or an app cannot be read.
    private async Task<bool> Fresh(AppOperationCatalog catalog)
    {
        try
        {
            var kernel = ServiceProvider.GetRequiredService<IKernel>();
            List<string> names = [];
            await foreach (var instance in kernel.InstancesIn(Scope, kernel.TypeOf(typeof(IApp))))
            { if (IApp.NameOf(Scope, instance.Key) is { } name) { names.Add(name); } }
            foreach (var name in catalog.Unknown(Scope, names))
            {
                var snapshot = await App(IApp.Key(Scope, name)).Read();
                catalog.Upsert(Scope, name, snapshot.MigrationTarget is not null || snapshot.Manifest is null ? []
                    : [.. snapshot.Manifest.Operations.Select(operation => new MenuOperation(name, operation.Name,
                        string.IsNullOrWhiteSpace(operation.Description) ? null : operation.Description))]);
            }
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            ServiceProvider.GetRequiredService<ILogger<AppOperationCatalog>>().LogSwallowed(error, "The app menu could not be refreshed; it is read again");
            return false;
        }
    }

    // The second tier's menu: the runtime tools this brain's sources registered (each a concrete instance
    // with its Key) and the contracts the caller named. The deployed catalog as a whole is never the menu,
    // and an app is reached only through its operations (the first tier), never by a raw IApp call.
    private async Task<NeuronType[]> Menu(string[]? allowed, CancellationToken ct)
        => [.. (await Capabilities().Capabilities(ct))
            .Where(type => type.Key is not null || (allowed?.Contains(type.Contract, StringComparer.Ordinal) ?? false))
            .Where(type => allowed is null || allowed.Contains(type.Contract, StringComparer.Ordinal))
            .Where(type => type.Contract != typeof(IApp).FullName)];

    // The app tier is IApp's place on the menu: it runs when the caller names no contracts, or names IApp.
    private static bool AppsAllowed(string[]? allowed) => allowed is null || allowed.Contains(typeof(IApp).FullName!, StringComparer.Ordinal);

    // The asking app, from the kernel-stamped caller (an app's behaviors call as their app's key).
    private string? Asker() => CallerContextStamper.TryGet(out var caller) && caller.AppId is { } app ? IApp.NameOf(Scope, app) : null;

    public async Task<RouteOutcome> Dispatch(string state, string[]? allowed, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        var route = Decided(state, allowed) ?? await Route(state, allowed, ct);
        if (route.Confidence < Gate || route.Method.Length == 0)
        { return new RouteOutcome(route, false, Error: NotClear); }
        if (route.App is not null)
        {
            var appKey = IApp.Key(Scope, route.App);
            var app = App(appKey);
            try
            {
                // An operation whose input has to be filled from the request is the agent turn's, with the route as its hint.
                if (await DirectInput(app, route.Operation!) is not { } input) { return new RouteOutcome(route, false, appKey, Error: NeedsInput); }
                var requestId = await app.Request(route.Operation!, input);
                return new RouteOutcome(route, true, appKey, null, requestId.ToString());
            }
            catch (Exception error) when (error is not OperationCanceledException)
            { return new RouteOutcome(route, false, appKey, null, Error: error.Message); }
        }
        var (key, arguments) = await Fill(state, route, ct);
        try
        {
            var result = await ServiceProvider.GetRequiredService<NeuronSurface>().Call(route.Contract, key, route.Method, arguments, ct);
            return new RouteOutcome(route, true, key, arguments, result?.GetRawText());
        }
        catch (Exception error) when (error is not OperationCanceledException)
        { return new RouteOutcome(route, false, key, arguments, Error: error.Message); }
    }

    // The route this router already decided for the same request (IRouter.Ask), when this caller may carry it
    // out: Dispatch carries out the route the caller was shown instead of deciding again. Only the latest ask
    // of the request counts; one still routing, or decided with no route, is decided again.
    private RouteResult? Decided(string state, string[]? allowed)
    {
        var request = Clip(state);
        return Snapshot.Asked.LastOrDefault(item => item.State == request) is { Status: "Routed", Result: { } route }
            && Offers(route, allowed) && (route.App is null || route.App != Asker()) ? route : null;
    }

    // Whether a route is on the menu the caller's allowed list builds: an app route through the app tier, a
    // runtime tool unless the list leaves its contract out, any other contract only when the list names it.
    private static bool Offers(RouteResult route, string[]? allowed)
        => route.App is not null ? AppsAllowed(allowed)
            : route.Key is not null ? allowed is null || allowed.Contains(route.Contract, StringComparer.Ordinal)
            : allowed?.Contains(route.Contract, StringComparer.Ordinal) ?? false;

    // An app operation's input when no language model has to fill it: "open" takes "" (what the shell posts to
    // open an app), and so does an operation on a toolbar button (AppToolbarEntry). Any other input is filled
    // from the request.
    private static async Task<string?> DirectInput(IApp app, string operation)
    {
        if (operation == "open") { return ""; }
        var toolbar = (await app.Read()).Manifest.Toolbar ?? [];
        return toolbar.Any(entry => entry.Operation == operation && entry.Kind == AppToolbarKinds.Button) ? "" : null;
    }

    // The assistant: achieve a goal across several actions. Each step Jev picks the next capability
    // (or says the goal is done), a language model fills its call, it runs, and its result feeds the
    // next decision. The catalog includes ICSharpFile and the machine console/files, so the goal can
    // be reached by writing C# or by running commands and touching files on the PC.
    public async Task<GoalRun> Achieve(string goal, string[]? allowed, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        var types = (await Menu(allowed, ct)).ToArray();
        if (types.Length == 0) { throw new InvalidOperationException("No contracts are available to route over."); }
        var byId = types.ToDictionary(type => type.Id, type => type, StringComparer.Ordinal);
        var systemone = SiloNeurons.Get<ISystemOne>(ServiceProvider, ISystemOne.Key(Scope));
        var surface = ServiceProvider.GetRequiredService<NeuronSurface>();
        var steps = new List<GoalStep>();
        for (var index = 0; index < MaxSteps; index++)
        {
            var moment = AwareOfState(goal, steps);
            var criteria = types.ToDictionary(type => type.Id, type => (string?)Trim(type.Description), StringComparer.Ordinal);
            criteria["finish"] = "The goal is complete, or cannot be taken further. Choose this to stop.";
            var stepAnswer = await systemone.Ask(new SystemOneRequest(moment,
                new() { ["next"] = new SystemOneQuestion.Choice("What should be done next to achieve the goal?", criteria) }), ct);
            if (Best(stepAnswer, criteria.Keys) is not { } pick) { break; }
            var (chosenId, confidence, _) = pick;
            if (chosenId == "finish" || confidence < Gate || !byId.TryGetValue(chosenId, out var chosen)) { break; }
            string method;
            if (chosen.Key is not null)
            {
                method = "Run";
            }
            else
            {
                var methods = chosen.Methods.Select(MethodName).Where(name => !NotCallable.Contains(name)).Distinct(StringComparer.Ordinal).ToArray();
                if (methods.Length == 0) { break; }
                var methodAnswer = await systemone.Ask(new SystemOneRequest(moment,
                    new() { ["method"] = new SystemOneQuestion.Choice("Which method carries out this step?", methods.ToDictionary(name => name, _ => (string?)null, StringComparer.Ordinal)) }), ct);
                method = Best(methodAnswer, methods)?.Id ?? methods[0];
            }
            try
            {
                var route = new RouteResult(chosen.Contract, method, confidence, new(), chosen.Key, chosen.Tool);
                var (key, arguments) = await Fill(moment, route, ct);
                var result = await surface.Call(chosen.Contract, key, method, arguments, ct);
                steps.Add(new GoalStep(chosen.Contract, method, key, arguments, result?.GetRawText(), null, confidence));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            { steps.Add(new GoalStep(chosen.Contract, method, null, null, null, error.Message, confidence)); }
        }
        if (steps.Count > 0) { await Remember(goal, steps[^1].Contract, steps[^1].Method); }
        var last = steps.LastOrDefault();
        return new GoalRun(goal, [.. steps], last?.Error ?? last?.Result);
    }

    // The router's own state: what it decided before, shown to the model so a follow-up is judged
    // in context. Its memory is its grain state, never a process.
    private string AwareOfState(string request)
    {
        var recent = Snapshot.Recent;
        if (recent.Length == 0) { return request; }
        var builder = new StringBuilder("Earlier requests and what they routed to:\n");
        foreach (var record in recent) { builder.AppendLine($"- \"{Clip(record.Request)}\" -> {record.Contract}.{record.Method}"); }
        builder.AppendLine().Append("Request: ").Append(Clip(request));
        return builder.ToString();
    }

    // Everything already done for the current goal, so each step is decided against the observations the
    // earlier steps produced. The router's memory of earlier requests stays out: a goal asked again would
    // read as already done, and its run would end with no step.
    private static string AwareOfState(string goal, IReadOnlyList<GoalStep> steps)
    {
        var builder = new StringBuilder();
        if (steps.Count > 0)
        {
            builder.AppendLine("What you have done so far for this goal:");
            foreach (var step in steps)
            { builder.AppendLine($"- {step.Contract}.{step.Method} with {Clip(step.Arguments ?? "[]")} -> {Clip(step.Error ?? step.Result ?? "")}"); }
            builder.AppendLine();
        }
        builder.Append("Goal: ").Append(Clip(goal));
        return builder.ToString();
    }

    private async Task Remember(string request, string contract, string method)
    {
        var recent = Recorded(request, contract, method);
        await Save(Snapshot with { Recent = recent }, new RouterDecided(Key, recent.Length));
    }

    private RouteRecord[] Recorded(string request, string contract, string method)
    {
        var record = new RouteRecord(Clip(request), contract, method);
        return [.. Snapshot.Recent.Where(item => item.Request != record.Request || item.Contract != record.Contract).TakeLast(MaxRecent - 1), record];
    }

    // A language model fills the two things a System One model cannot produce: the grain key and the
    // argument array. This is the hybrid: Jev decides what, the language model supplies the strings.
    private async Task<(string Key, string Arguments)> Fill(string state, RouteResult route, CancellationToken ct)
    {
        var catalog = ServiceProvider.GetRequiredService<ModelCatalog>();
        var resolved = await catalog.Resolve(Scope, null, false, ct);
        using var client = await catalog.CreateClient(Scope, resolved, ct);
        var entry = route.Key is null ? null : await FindEntry(route, ct);
        var signature = entry?.Methods.FirstOrDefault() ?? await Signature(route, ct);
        var instructions = entry is null
            ? "Fill one neuron call from the user's request. Reply with JSON only: {\"key\": \"<grain key>\", \"arguments\": [ ... ]}.\n"
                + $"Contract: {route.Contract}\nMethod: {signature}\n"
                + $"The brain scope is {Scope}. An app is {Scope}/apps/{{name}}; a connection is {Scope}/connection/{{connector}}/{{id}}.\n"
                + "arguments is a JSON array, one element per method parameter, omitting CancellationToken. Use null for an omitted optional parameter."
            : "Fill the arguments for the chosen tool from the user's request. Reply with JSON only: {\"arguments\": {...}}.\n"
                + $"Contract: {route.Contract}\nTool: {route.Tool}\n{signature}\n"
                + "arguments is a JSON object matching the tool's schema, one property per argument. Use null for an omitted optional field.";
        var options = new ChatOptions { Instructions = instructions, ResponseFormat = ChatResponseFormat.Json };
        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, state)], options, ct);
        using var document = JsonDocument.Parse(response.Text);
        if (route.Key is not null)
        {
            var argumentsObject = document.RootElement.TryGetProperty("arguments", out var toolArguments) && toolArguments.ValueKind == JsonValueKind.Object ? toolArguments.GetRawText() : "{}";
            return (route.Key, $"[{JsonSerializer.Serialize(route.Tool)}, {JsonSerializer.Serialize(argumentsObject)}]");
        }
        var key = document.RootElement.TryGetProperty("key", out var keyValue) && keyValue.ValueKind == JsonValueKind.String ? keyValue.GetString() : null;
        if (string.IsNullOrWhiteSpace(key)) { throw new InvalidOperationException("The model did not supply a neuron key."); }
        var arguments = document.RootElement.TryGetProperty("arguments", out var args) ? args.GetRawText() : "[]";
        return (key!, arguments);
    }

    private async Task<NeuronType?> FindEntry(RouteResult route, CancellationToken ct)
    {
        if (route.Key is null) { return null; }
        var entries = await Capabilities().Capabilities(ct);
        return entries.FirstOrDefault(type => type.Contract == route.Contract && type.Key == route.Key && type.Tool == route.Tool);
    }

    private async Task<string> Signature(RouteResult route, CancellationToken ct)
    {
        var type = (await ServiceProvider.GetRequiredService<IKernel>().Types(ct)).SingleOrDefault(item => item.Kind == DigitalBrain.TypeKind.Neuron && item.Name == route.Contract);
        return type?.Shape.FirstOrDefault(method => MethodName(method) == route.Method) ?? route.Method;
    }

    // The offered option the answers' own distributions rank highest, with that option's probability as its
    // confidence. A System One answer's Value and Confidence can contradict its distribution or name an option
    // that was never offered (seen live from the local adapter), so the Value only breaks a tie; an answer
    // with no distribution keeps its Value and Confidence. Null when no answer names an offered option.
    private static Pick? Best(SystemOneResult result, ICollection<string> offered)
    {
        Pick? best = null;
        foreach (var answer in result.Answers.Values)
        {
            if (answer is not SystemOneAnswer.Choice choice) { continue; }
            var ranked = choice.Probabilities.Where(pair => offered.Contains(pair.Key)).ToDictionary(StringComparer.Ordinal);
            if (choice.Probabilities.Count == 0 && offered.Contains(choice.Value)) { ranked[choice.Value] = choice.Confidence; }
            if (ranked.Count == 0) { continue; }
            var top = ranked.OrderByDescending(pair => pair.Value).ThenByDescending(pair => pair.Key == choice.Value).First();
            if (best is null || top.Value > best.Confidence) { best = new Pick(top.Key, top.Value, ranked); }
        }
        return best;
    }

    private sealed record Pick(string Id, double Confidence, Dictionary<string, double> Probabilities);

    // No offered option fits: Dispatch refuses it, an asked route is answered with no result, and the assistant
    // hands the request to the agent turn.
    private static RouteResult Unclear() => new("", "", 0, new());

    private static IEnumerable<Dictionary<string, string?>> Chunk(Dictionary<string, string?> all)
    {
        var buffer = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var pair in all)
        {
            buffer[pair.Key] = pair.Value;
            if (buffer.Count == MaxOptions) { yield return buffer; buffer = new(StringComparer.Ordinal); }
        }
        if (buffer.Count > 0) { yield return buffer; }
    }

    private static string MethodName(string declared)
    {
        var signature = declared.Split(" — ", 2, StringSplitOptions.None)[0].Trim();
        var paren = signature.IndexOf('(');
        if (paren > 0) { signature = signature[..paren]; }
        var space = signature.LastIndexOf(' ');
        return space >= 0 ? signature[(space + 1)..] : signature;
    }

    private static string Trim(string description) => description.Length <= 200 ? description : description[..200];

    private static string Clip(string request) => request.Length <= MaxRequestChars ? request : request[..MaxRequestChars];
}
