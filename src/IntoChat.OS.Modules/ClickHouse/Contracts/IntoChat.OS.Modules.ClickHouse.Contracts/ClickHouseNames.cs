namespace IntoChat.OS.Modules.ClickHouse;

// The module's addressable neuron vocabulary.
public static class ClickHouseNames
{
    public const string NeuronType = "clickhouse";
    // The integration neuron serving the deployment's database lives in the system brain (06-08: a plain key names its brain).
    public const string DefaultNeuron = "system/default";
}
