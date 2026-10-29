using System.Text.Json;
using DigitalBrain;
using DigitalBrain.Runtime.Orleans;
using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using DigitalBrain.Spi;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Catalog;

public sealed record CSharpContractModule(string Id);

public sealed record CSharpContractCatalogSnapshot(IReadOnlyList<CSharpContractModule> Modules, IReadOnlyList<string> Contracts, string Example, bool Truncated = false);

// What a script author (human or agent) sees: the kernel's types for the caller's brain, by provider.
public sealed class CSharpContractDiscovery(IBrainObserver kernel, ModuleInventory inventory)
{
    private const int ResponseBudget = 24000;

    public const string Example = """
        // Plain C#: every installed contract is referenced; name the ones you use with usings.
        // System, collections, LINQ, tasks, IntoChat.OS and IntoChat.OS.Client are imported.
        using IntoChat.OS.Modules.Flutter.Button;
        using IntoChat.OS.Modules.Flutter.Button.Signals;

        await using var brain = await IntoChatOSClient.ConnectAsync(args);
        var button = brain.Get<IButton>(brain.Setting("ButtonId") ?? "start");
        await foreach (var click in brain.On<ButtonClicked>(button, brain.Stopping))
        {
            Console.WriteLine($"ButtonClicked {click.Name} {click.Action}");
        }
        // Top-level statements run once; loop over brain.On<T>() to keep reacting to signals. A behavior stays
        // loaded in its brain's session; signals that arrive while it reloads are buffered, not lost.
        // Console output is the log. Settings arrive through brain.Setting(name).
        """;

    public async Task<CSharpContractCatalogSnapshot> Read(IReadOnlyList<string> moduleIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleIds);
        var types = await Types(cancellationToken);
        var installed = types.Where(type => type.Kind == DigitalBrain.TypeKind.Neuron).GroupBy(type => type.Origin, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(module => module.Key, module => module.ToArray(), StringComparer.OrdinalIgnoreCase);
        var unknown = moduleIds.Where(id => !installed.ContainsKey(id)).ToArray();
        if (unknown.Length > 0)
        { throw new ArgumentException($"Unknown module IDs: {string.Join(", ", unknown)}. Installed module IDs: {string.Join(", ", installed.Keys.Order(StringComparer.Ordinal))}."); }
        var modules = installed.OrderBy(module => module.Key, StringComparer.Ordinal)
            .Select(module => new CSharpContractModule(module.Key)).ToArray();
        var groups = moduleIds.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)
            .SelectMany(id => types.Where(type => string.Equals(type.Origin, id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(type => type.Name, StringComparer.Ordinal).SelectMany(type => Groups(type, null))).ToArray();
        return Bounded(modules, groups);
    }

    public async Task<CSharpContractCatalogSnapshot> ReadDetail(string moduleId, string contractName, string? methodName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractName);
        if (methodName is not null) { ArgumentException.ThrowIfNullOrWhiteSpace(methodName); }
        var types = await Types(cancellationToken);
        var modules = types.Where(type => type.Kind == DigitalBrain.TypeKind.Neuron).Select(type => type.Origin).Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal).Select(id => new CSharpContractModule(id)).ToArray();
        if (!modules.Any(module => string.Equals(module.Id, moduleId, StringComparison.OrdinalIgnoreCase)))
        { throw new ArgumentException($"Unknown module ID '{moduleId}'."); }
        var roots = types.Where(type => string.Equals(type.Origin, moduleId, StringComparison.OrdinalIgnoreCase) && type.Name == contractName).ToArray();
        if (roots.Length != 1) { throw new ArgumentException($"Contract '{contractName}' does not identify one installed contract in module '{moduleId}'."); }
        return Bounded(modules, Groups(roots[0], methodName).ToArray());
    }

    private async Task<BrainType[]> Types(CancellationToken ct) => (await kernel.Types(ct).ConfigureAwait(false))
        .Where(type => type.Kind is DigitalBrain.TypeKind.Neuron or DigitalBrain.TypeKind.Signal).ToArray();

    private IEnumerable<Declaration> Groups(BrainType entry, string? methodName)
    {
        var writer = new ContractDeclarationWriter(inventory);
        var contract = writer.Resolve(entry.Origin, entry.Name);
        if (entry.Kind == DigitalBrain.TypeKind.Signal)
        {
            if (methodName is not null) { throw new ArgumentException("A signal has no callable methods.", nameof(methodName)); }
            yield return new(entry.Origin + "/" + entry.Name, writer.Write(contract, null, "// signal " + entry.Name));
            yield break;
        }
        var methods = ContractDeclarationWriter.Methods(contract);
        if (methodName is not null)
        {
            methods = methods.Where(method => method.Name == methodName).ToArray();
            if (methods.Length == 0) { throw new ArgumentException($"Unknown method '{methodName}' on '{entry.Name}'.", nameof(methodName)); }
        }
        foreach (var method in methods)
        {
            var selector = entry.Origin + "/" + entry.Name + "/" + method.Name;
            yield return new(selector, writer.Write(contract, method, $"// detail: {selector}\n{entry.Name} [neuron id: {entry.Alias}/<key>] — {entry.Description}"));
        }
    }

    private static CSharpContractCatalogSnapshot Bounded(CSharpContractModule[] modules, Declaration[] groups)
    {
        // Each omitted selector remains usable with ReadDetail; never clip the method's DTO closure.
        for (var count = groups.Length; count >= 0; count--)
        {
            var contracts = groups.Take(count).Select(group => group.Text).ToList();
            if (count < groups.Length)
            { contracts.Add("// Omitted detail selectors (moduleId/contractName/methodName):\n" + string.Join("\n", groups.Skip(count).Select(group => group.Selector))); }
            var snapshot = new CSharpContractCatalogSnapshot(modules, contracts, Example, count < groups.Length);
            if (JsonSerializer.Serialize(snapshot).Length <= ResponseBudget)
            {
                if (groups.Length == 1 && count == 0)
                { throw new InvalidOperationException("The complete contract schema exceeds the 24000-character discovery response limit."); }
                return snapshot;
            }
        }
        throw new InvalidOperationException("The discovery index exceeds the 24000-character response limit; select fewer modules or one contract method.");
    }

    private sealed record Declaration(string Selector, string Text);
}
