using IntoChat.OS.Modules.AI.Models;

namespace IntoChat.OS.Modules.AI.Embeddings;

// Marker root for embedding model markers, each resolved as a keyed IEmbeddingGenerator service.
// Scripts embed through the IEmbeddingModel neuron (the deployment default) and rank with IVectorIndex.
public interface IEmbedding : IAiMarker;
