namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// Transitional (06-07, R4-15, deleted in 06-08): a script subscription to a plain [Neuron] names its source
// "plain:{brain}/{type}/{key}", so the runtime tells it from a grain id and hears it through the session synapse neuron
// instead of an observer. Its Signal is the source's [Signal] alias, and its SourceSeq the publisher sequence in the BrainLog.
internal static class PlainSources
{
    public const string Prefix = "plain:";

    public static string Of(DigitalBrain.NeuronId id) => Prefix + id;

    public static bool TryParse(string neuron, out DigitalBrain.NeuronId id)
    {
        id = default;
        return neuron.StartsWith(Prefix, StringComparison.Ordinal) && DigitalBrain.NeuronId.TryParse(neuron[Prefix.Length..], out id);
    }

    public static bool Is(string neuron) => TryParse(neuron, out _);

    // One session per program: the source's AdmitSynapse judges "{brain}/runtime.session/csharp/{owner}".
    public static string SessionOf(string owner) => "csharp/" + owner;
}
