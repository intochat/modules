using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.AI.Models.Signals;

[Signal("ai.inference-started")]
public sealed record InferenceStarted(string NeuronId, string OperationId) : DigitalBrain.Signal;
[Signal("ai.inference-completed")]
public sealed record InferenceCompleted(string NeuronId, string OperationId) : DigitalBrain.Signal;
[Signal("ai.inference-failed")]
public sealed record InferenceFailed(string NeuronId, string OperationId,
    string Reason) : DigitalBrain.Signal;
