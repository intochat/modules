using DigitalBrain;
using IntoChat.OS;

namespace IntoChat.OS.Modules.Microsoft.Orleans;

[Signal("orleans.cluster.changed")]
public sealed record ClusterChanged(ClusterSnapshot Snapshot) : DigitalBrain.Signal;
