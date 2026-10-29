using DigitalBrain.Contracts;
using DigitalBrain;
using System.ComponentModel;
using IntoChat.OS;

namespace IntoChat.OS.Modules.AI.Embeddings;

// The caller embeds (IEmbeddingModel) and the index ranks. An empty index takes its dimensions from the next
// write; while it holds entries, every vector it is given or asked about must have those dimensions.
[NeuronAlias("ai.vector-index"), DefaultNeuronType("ai.vector-index")]
[Description("A brain's nearest-neighbour memory, keyed {brain}/memory/{name}: entries of id, vector and payload, ranked by cosine similarity.")]
public interface IVectorIndex
{
    [Description("Rebuild the index from exactly these entries; entries not given are dropped and the dimensions are taken from them.")]
    Task<VectorIndexSnapshot> Replace(VectorEntry[] entries);

    [Description("Add these entries, replacing any stored entry with the same id.")]
    Task<VectorIndexSnapshot> Upsert(VectorEntry[] entries);

    [Description("Drop the entries with these ids; unknown ids are ignored.")]
    Task<VectorIndexSnapshot> Remove(string[] ids);

    [Description("The take entries nearest the query by cosine similarity, best first, among those whose payload holds every mustMatch pair.")]
    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<VectorMatch[]> Nearest(float[] query, int take, Dictionary<string, string>? mustMatch = null);

    [Description("How many entries the index holds, their dimensions and its revision.")]
    [DigitalBrain.ReadOnlyCall]
    Task<VectorIndexSnapshot> Read();

    static string Key(string brainId, string name) => $"{brainId}/memory/{name}";
}

public sealed record VectorEntry(
    string Id,
    float[] Vector,
    Dictionary<string, string> Payload);

public sealed record VectorMatch(
    string Id,
    double Score,
    Dictionary<string, string> Payload);

public sealed record VectorIndexSnapshot(
    int Count,
    int Dimensions,
    long Revision);
