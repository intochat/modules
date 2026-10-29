using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Connections.Contracts;
using DigitalBrain.Runtime.Orleans;
using DigitalBrain.Runtime.Orleans.Hosting;
using DigitalBrain.Spi;

namespace IntoChat.OS.Modules.Tools;

// A brain's tools as capabilities: every existing instance of a kernel neuron type that is a tool
// source, asked for its live tools, each one bound to its source's key and called as Run(tool, arguments).
// Lists are clipped so one misbehaving source cannot flood search.
public sealed class ToolCapabilitySource(IBrainObserver kernel, IServiceProvider? plain = null) : IRuntimeCapabilitySource
{
    internal const int MaxTools = 200;
    internal const int MaxDescription = 1000;
    // A source is a plain contract deriving IToolSource (06-06d), listed by the kernel's instance directory and reached through
    // the plain-neuron seam. The kernel's Implements names neuron contracts only, so the plain contract is resolved by name.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Type?> PlainSources = new(StringComparer.Ordinal);

    public async Task<IReadOnlyList<NeuronType>> Read(string brainId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(brainId);
        if (BrainScope.CurrentId() != brainId) { throw new UnauthorizedAccessException($"Tools are read for the caller's own brain, not '{brainId}'."); }
        List<NeuronType> capabilities = [];
        foreach (var source in (await kernel.Types(ct)).Where(type => type.Kind == DigitalBrain.TypeKind.Neuron))
        {
            var contract = plain is null ? null : PlainSourceOf(source.Name);
            if (contract is null) { continue; }
            await foreach (var instance in kernel.Instances(source.Alias, ct))
            {
                ToolSet set;
                // One source that cannot answer drops out of search; the others stay.
                try
                {
                    var tools = (IToolSource)SiloNeurons.Get(plain!, contract, instance.Key);
                    set = await tools.Tools(ct).WaitAsync(ct);
                }
                catch (Exception error) when (error is not OperationCanceledException) { continue; }
                capabilities.AddRange(set.Tools.Take(MaxTools).Select(tool => Capability(instance.Key, source.Name, set.Title, tool)));
            }
        }
        return capabilities;
    }

    // The plain contract of that name when it is a tool source, else null.
    private static Type? PlainSourceOf(string name) => PlainSources.GetOrAdd(name, static name =>
        AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic)
            .Select(assembly => assembly.GetType(name, throwOnError: false))
            .FirstOrDefault(type => type is { IsInterface: true } && typeof(IToolSource).IsAssignableFrom(type) && IntoChat.OS.Runtime.PlainNeuronContracts.Is(type)));

    internal static NeuronType Capability(string key, string contract, string title, ToolDescriptor tool)
    {
        var description = tool.Description.Length <= MaxDescription ? tool.Description : tool.Description[..MaxDescription];
        return new NeuronType(
            $"tool:{key}/{tool.Name}", "tools", $"{title}: {tool.Title}", description,
            [$"Task<ToolResult> Run(\"{tool.Name}\", arguments) — call this source's Tools() for the full input schema before supplying arguments."],
            contract, key, tool.Name);
    }
}
