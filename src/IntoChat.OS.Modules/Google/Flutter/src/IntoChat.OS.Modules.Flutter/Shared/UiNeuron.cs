namespace IntoChat.OS.Modules.Flutter;

// A Flutter control as a plain DigitalBrain neuron (06-04): the members the controls used on the Framework neuron, so the
// migration changes their identity and base only. Snapshot is the current state object, as it was on the Framework neuron;
// FullKey is the os key "{brain}/{key}" the controls name themselves by in state and signals (the shell, scripts and handlers
// match on it).
internal abstract class UiNeuron<TState> : DigitalBrain.Neuron<TState> where TState : class, new()
{
    protected TState Snapshot => State;

    protected string FullKey => $"{Id.Brain.Value}/{Id.Key}";

    protected IServiceProvider ServiceProvider => Context.Services;

    // The type of the control's UI binding, a plain neuron of the same key (06-08).
    internal const string BindingType = "ui.binding";

    // Sends a UI event to the handler bound to this control. The binding sees this control as the caller, and the os caller stamp
    // of the call that raised the event flows on to the handler (UiEvent.Caller). An unbound control has no handler to tell.
    protected Task Dispatch(UiEvent uiEvent)
        => Get<IUiBinding>(new DigitalBrain.NeuronId(Id.Brain, BindingType, Id.Key)).Dispatch(uiEvent);
}
