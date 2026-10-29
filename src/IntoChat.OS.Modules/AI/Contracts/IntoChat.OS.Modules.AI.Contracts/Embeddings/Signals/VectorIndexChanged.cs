using System.ComponentModel;
using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.AI.Embeddings.Signals;

[Signal("ai.vector-index-changed")]
[Description("A vector index's entries changed: its key, its new revision and how many entries it holds.")]
public sealed record VectorIndexChanged(
    string Index,
    long Revision,
    int Count) : DigitalBrain.Signal;
