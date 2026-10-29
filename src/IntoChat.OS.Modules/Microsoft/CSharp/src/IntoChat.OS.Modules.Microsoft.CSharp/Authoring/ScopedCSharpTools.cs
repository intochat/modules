using DigitalBrain.Runtime.Orleans;
using DigitalBrain.Spi;
using IntoChat.OS.Runtime;
using System.ComponentModel;
using System.Reflection;
using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Modules.Microsoft.CSharp;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
using IntoChat.OS.Modules.Microsoft.CSharp.Files;
using ModelContextProtocol.Server;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Authoring;

[McpServerToolType]
public sealed class ScopedCSharpTools(INeuronProxies brain, IBrainObserver kernel, CSharpContractDiscovery? contracts, string scope, bool canRun)
{
    private const int LogTail = 150;

    public bool CanRun => canRun;

    [McpServerTool(Name = "csharp_contracts"), Description("Discover installed module IDs, neuron contracts with their signals and an example single-file C# app. Pass modules=[] first, then select exact returned IDs (for example time and flutter).")]
    public Task<CSharpContractCatalogSnapshot> Contracts(string[] modules, CancellationToken ct) => RequireModule().Read(modules, ct);

    [McpServerTool(Name = "csharp_contract_detail"), Description("Read one installed contract and its complete request/result declarations. Use exact moduleId and contractName from discovery; pass methodName=null for the contract, or a method name to narrow a truncated result. Never author a call from an omitted schema.")]
    public Task<CSharpContractCatalogSnapshot> ContractDetail(string moduleId, string contractName, string? methodName, CancellationToken ct)
        => RequireModule().ReadDetail(moduleId, contractName, methodName, ct);

    [McpServerTool(Name = "csharp_write"), Description("Save the full source of a single-file C# app and its readable name and purpose. The app connects with `await using var brain = await IntoChatOSClient.ConnectAsync(args);` and operates neurons through brain.Get<T>(id) and brain.On<TSignal>(neuron). Name the contracts you use with plain usings. Saving does not restart a running app; use csharp_run start.")]
    public async Task<CSharpFileView> Write(string id, string source, string? name, string? purpose, CancellationToken ct)
    {
        // Name and purpose are refused before any source is stored.
        if (name is { Length: > 120 } || purpose is { Length: > 4000 })
        { throw new ArgumentException("A name is at most 120 characters and a purpose at most 4000."); }
        var file = File(id);
        await file.Write(source, ct);
        await file.Describe(name, purpose, ct);
        return CSharpFileView.Of(id, await file.Inspect(ct));
    }

    [McpServerTool(Name = "csharp_run"), Description("Act on a saved C# app: status reads its run state and recent output; start compiles and runs the current source in the C# sandbox (restarting it if running) and keeps it running: a crash is retried up to 5 times and a lost sandbox is restarted; stop stops it; logs reads recent console output; delete stops it and removes the file. Compile errors appear in logs with a non-zero exit code while the status is Restarting, then Exited. Never claim the app works until its logs show the expected output.")]
    public async Task<CSharpFileView> Run(string id, string action, CancellationToken ct) => (action ?? "").Trim().ToLowerInvariant() switch
    {
        "status" or "logs" => await Read(id, ct),
        "start" => await Start(id, ct),
        "stop" => await Stop(id, ct),
        "delete" => await Delete(id, ct),
        _ => throw new ArgumentException("Use action status, start, stop, logs or delete.", nameof(action)),
    };

    // What exists is the kernel's: every C# file neuron of this brain with persisted state.
    public async Task<IReadOnlyList<CSharpFileView>> List(CancellationToken ct)
    {
        var prefix = CSharpFileKeys.For(scope, "_")[..^1];
        var views = new List<CSharpFileView>();
        await foreach (var instance in kernel.Instances<ICSharpFile>(ct))
        {
            if (!instance.Key.StartsWith(prefix, StringComparison.Ordinal)) { continue; }
            var id = instance.Key[prefix.Length..];
            views.Add(CSharpFileView.Of(id, await File(id).Inspect(ct)));
        }
        return [.. views.OrderBy(view => view.Description.Name, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<CSharpFileView> Read(string id, CancellationToken ct)
    {
        var file = await Existing(id, ct);
        return CSharpFileView.Of(id, await file.Inspect(ct), await file.ReadLogs(LogTail, ct));
    }

    public async Task<CSharpFileView> Start(string id, CancellationToken ct)
    {
        if (!canRun) { throw new InvalidOperationException("This host has no C# sandbox, so it cannot run C# files."); }
        var file = await Existing(id, ct);
        return CSharpFileView.Of(id, await file.Start(ct));
    }

    public async Task<CSharpFileView> Stop(string id, CancellationToken ct)
    {
        var file = await Existing(id, ct);
        return CSharpFileView.Of(id, await file.Stop(ct));
    }

    public async Task<CSharpFileView> Delete(string id, CancellationToken ct)
    {
        var file = await Existing(id, ct);
        var described = await file.Inspect(ct);
        await file.Delete(ct);
        return CSharpFileView.Of(id, described with { Status = "Stopped", ShouldRun = false });
    }

    private async Task<ICSharpFile> Existing(string id, CancellationToken ct)
    {
        var file = File(id);
        return (await file.Inspect(ct)).Created is null ? throw new KeyNotFoundException($"{id} is not a C# file in this workspace.") : file;
    }

    private ICSharpFile File(string id)
    {
        RequireModule();
        var key = CSharpFileKeys.For(scope, id);
        // app-{digest} names are the Apps module's program keys; authoring never describes or reaches one.
        if (id.StartsWith("app-", StringComparison.Ordinal)) { throw new ArgumentException("Names starting with app- are reserved for app programs.", nameof(id)); }
        return brain.Get<ICSharpFile>(key);
    }

    private CSharpContractDiscovery RequireModule()
        => contracts ?? throw new InvalidOperationException("This host does not run C# files; compose CSharpModule.");
}
