namespace IntoChat.OS.Modules.AI;

// An AI neuron as a plain DigitalBrain neuron (06-05): the members the AI neurons used on the Framework neuron, so the
// migration changes their identity and base only. FullKey is the os key "{brain}/{key}" the neurons name themselves by in
// state and signals (scripts and handlers match on it).
internal abstract class AiNeuron<TState> : DigitalBrain.Neuron<TState> where TState : class, new()
{
    protected TState Snapshot => State;

    protected string FullKey => $"{Id.Brain.Value}/{Id.Key}";

    protected IServiceProvider ServiceProvider => Context.Services;
}

// The stateless form of AiNeuron<TState>.
internal abstract class StatelessAiNeuron : DigitalBrain.Neuron
{
    protected string FullKey => $"{Id.Brain.Value}/{Id.Key}";

    protected IServiceProvider ServiceProvider => Context.Services;
}
