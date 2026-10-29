using System.Reflection;
using System.Text.RegularExpressions;
using IntoChat.OS.Modules.Flame.Scene;

namespace IntoChat.OS.Modules.Flutter.Tests.Integration.Contracts;

// W2 PR 0 (ui-contract 7.1): until the generator replaces them (PR 3), the shell's hand-written mirrors of the C#
// vocabulary are checked against it here: RendererRegistry.uiKinds ("Mirrors UIVocabulary") and the NodeContracts
// table the shell watches by contract full name and change-signal short name.
public sealed partial class DartVocabularyFacts
{
    // Kinds the C# vocabulary declares that the renderer registry does not list: the shell draws chat and composer
    // through the workspace, not NeuronView; a viewport is a window onto another app's view, so it has no standalone
    // gallery sample. Confirmation is a host-owned inbox rendered through the view tree, never a gallery-owned neuron.
    private static readonly string[] CSharpOnlyKinds = ["chat", "composer", "viewport", UIVocabulary.ConfirmationType];

    internal static string[] Vocabulary() => [.. typeof(UIVocabulary).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string) && field.Name.EndsWith("Type", StringComparison.Ordinal))
        .Select(field => (string)field.GetRawConstantValue()!).Order(StringComparer.Ordinal)];

    internal static string[] RendererKinds()
    {
        var list = UiKinds().Match(UiContractFiles.Dart("ui", Path.Combine("composition", "renderer_registry.dart")));
        Assert.True(list.Success, "RendererRegistry.uiKinds was not found.");
        var body = list.Groups["body"].Value;
        var kinds = Quoted().Matches(body).Select(match => match.Groups["value"].Value).ToArray();
        // Every comma-separated entry must be a plain quoted kind; anything else is a shape this reader does not know.
        var entries = body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        Assert.True(kinds.Length == entries, $"RendererRegistry.uiKinds has {entries} entries but {kinds.Length} parsed as quoted kinds.");
        return [.. kinds.Order(StringComparer.Ordinal)];
    }

    internal static (string Kind, string Contract, string Changed)[] NodeContracts()
    {
        var source = UiContractFiles.Dart("core", "node_contracts.dart");
        var table = NodeContract().Matches(source)
            .Select(match => (match.Groups["kind"].Value, match.Groups["contract"].Value, match.Groups["changed"].Value)).ToArray();
        // Every NodeContract(...) construction in the table must parse; the class declaration's constructor does not count.
        var entries = Regex.Count(source, @"NodeContract\(") - Regex.Count(source, @"const NodeContract\(");
        Assert.True(table.Length == entries, $"node_contracts.dart has {entries} NodeContract entries but {table.Length} parsed.");
        return table;
    }

    [Fact]
    public void TheRendererRegistryMirrorsTheVocabulary()
    {
        Assert.Equal(Vocabulary().Except(CSharpOnlyKinds), RendererKinds());
    }

    [Fact]
    public void EveryNodeContractNamesAVocabularyKindItsContractAndItsChangeSignal()
    {
        var table = NodeContracts();
        Assert.Equal(30, table.Length);
        var assemblies = new[] { typeof(UIVocabulary).Assembly, typeof(IScene).Assembly };
        var offenders = new List<string>();
        foreach (var (kind, contract, changed) in table)
        {
            if (!Vocabulary().Contains(kind)) { offenders.Add($"{kind}: not a UIVocabulary kind"); }
            if (kind == UIVocabulary.ConfirmationType)
            {
                // The runtime's framework signal is intentionally not declared beside its OS inbox adapter.
                Assert.Equal(typeof(IntoChat.OS.Confirmation.IConfirmationInbox).FullName, contract);
                Assert.Equal(nameof(DigitalBrain.Confirmation.ConfirmationRequested), changed);
                continue;
            }
            var type = assemblies.Select(assembly => assembly.GetType(contract)).SingleOrDefault(found => found is not null);
            if (type is null || !type.IsInterface || !IntoChat.OS.Runtime.PlainNeuronContracts.Is(type)) { offenders.Add($"{kind}: {contract} is not a neuron contract"); continue; }
            if (!type.Assembly.GetExportedTypes().Any(signal => typeof(DigitalBrain.Signal).IsAssignableFrom(signal) && signal.Name == changed && signal.Namespace!.StartsWith(type.Namespace!, StringComparison.Ordinal)))
            { offenders.Add($"{kind}: no signal {changed} beside {contract}"); }
        }
        Assert.True(offenders.Count == 0, "node_contracts.dart drifted from the C# contracts:\n" + string.Join('\n', offenders));
    }

    // Every kind a view or composition may reference (ViewValidation, CompositionValidation) and GET /apps/node serves:
    // the shell watches each by contract, so a missing entry throws in the browser (the desktop's windows and the
    // workspace canvas are viewports).
    [Fact]
    public void EveryReferencedKindHasANodeContract()
    {
        string[] referenced = [UIVocabulary.ConfirmationType, "viewport", "chat", "composer", "text", "button", "textfield", "select", "fileinput", "imagecanvas",
            "form", "table", "chart", "voiceinput", "webbrowser", "toggle", "progress", "infobar", "image", "expander",
            "surface", "layout", "collection", "card", "tabs", "view", "entrycontrol", "preference"];
        Assert.Empty(referenced.Except(NodeContracts().Select(entry => entry.Kind)));
    }

    // Renderer vocabulary kinds the shell has no node contract for yet (no Flutter contract is watched for them). Known gap,
    // pinned so the guard passes at head: a kind that gains a Dart entry must leave this list (checked below); a kind
    // emitted by a UiChildRef call site must never be added here.
    private static readonly string[] VocabularyKindsWithoutADartContract =
        ["calendar", "clock", "color", "graph", "map", "rating", "sheet", "slider", "tree", "video"];

    [Fact]
    public void EveryKindTheServerCanEmitHasADartNodeContract()
    {
        // Emittable = renderer vocabulary + the literal kind of every UiChildRef("<kind>", ...) in production source.
        var emitters = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var kind in Vocabulary()) { emitters[kind] = ["UIVocabulary"]; }
        foreach (var directory in UiContractFiles.ProductionSourceDirectories())
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(UiContractFiles.Root, file).Replace('\\', '/');
                if (relative.Split('/').Any(part => part is "bin" or "obj" or "tests" or "test") || relative.Contains(".Tests", StringComparison.Ordinal)) { continue; }
                foreach (Match call in LiteralUiChildRef().Matches(File.ReadAllText(file)))
                {
                    var kind = call.Groups["kind"].Value;
                    if (!emitters.TryGetValue(kind, out var files)) { emitters[kind] = files = []; }
                    files.Add(relative);
                }
            }
        }
        Assert.Contains("viewport", emitters.Keys); // the scan must see the Workspace's call site, or it is blind.
        var tableKinds = NodeContracts().Select(contract => contract.Kind).ToHashSet();
        var stale = VocabularyKindsWithoutADartContract.Where(tableKinds.Contains).ToArray();
        Assert.True(stale.Length == 0, "VocabularyKindsWithoutADartContract lists kinds that now have a Dart entry: " + string.Join(", ", stale));
        var calls = emitters.Where(entry => entry.Value.Any(source => source != "UIVocabulary")).Select(entry => entry.Key);
        Assert.Empty(calls.Intersect(VocabularyKindsWithoutADartContract));
        var missing = emitters.Where(entry => !tableKinds.Contains(entry.Key) && !VocabularyKindsWithoutADartContract.Contains(entry.Key)).Select(entry => $"{entry.Key} (emitted by {string.Join(", ", entry.Value)})").ToArray();
        Assert.True(missing.Length == 0, "Kinds the server can emit have no entry in node_contracts.dart:\n" + string.Join('\n', missing));
    }

    [GeneratedRegex(@"UiChildRef\(\s*""(?<kind>\w+)""")]
    private static partial Regex LiteralUiChildRef();

    [GeneratedRegex(@"static const List<String> uiKinds = \[(?<body>[^\]]*)\];")]
    private static partial Regex UiKinds();

    [GeneratedRegex(@"'(?<value>[^']*)'")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"'(?<kind>\w+)':\s*NodeContract\(\s*'(?<contract>[\w.]+)',\s*'(?<changed>\w+)',?\s*\)")]
    private static partial Regex NodeContract();
}
