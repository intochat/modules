using System.Text.Json;
using DigitalBrain.Capabilities;

namespace IntoChat.OS.Modules.Flutter;

// A UI event as the bound handler receives it (06-07b, R4-17): the control's signal as its CLR type name and JSON, and the
// caller the dispatch ran for. It crosses the runtime's JSON envelope (06-08: every handler is a plain neuron). The caller is a
// cross-check only: a handler authorizes on the stamped caller of the call it runs in, never on this field alone.
public sealed record UiEvent(string Type, string Json, CapabilityCaller? Caller)
{
    private static readonly JsonSerializerOptions Options = JsonSerializerOptions.Web;

    // The event of a control signal (an os Signal or a DigitalBrain [Signal] record), without a caller; the binding adds it.
    public static UiEvent Of(object signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        return new(signal.GetType().FullName!, JsonSerializer.Serialize(signal, signal.GetType(), Options), null);
    }

    public bool Is<T>() => Type == typeof(T).FullName;

    // The signal when it is a T, else null.
    public T? As<T>() where T : class => Is<T>() ? JsonSerializer.Deserialize<T>(Json, Options) : null;
}

// A plain contract (06-08) several neuron classes implement (the circle, the circle factory): a binding addresses its handler
// by the handler's runtime id, so the contract names no neuron type.
[DigitalBrain.Contracts.NeuronAlias("ui.event-handler")]
public interface IUiEventHandler
{
    [DigitalBrain.OneWayCall] Task HandleUiEvent(UiEvent uiEvent);
}

// A plain neuron (06-08).
[DigitalBrain.Contracts.NeuronAlias("ui.binding"), DigitalBrain.Contracts.DefaultNeuronType("ui.binding")]
public interface IUiBinding
{
    // The handler is a neuron of the binding's own brain, named by its runtime id text "brain/type/key" (D-05); another brain's
    // handler is refused.
    Task Bind(string handler);
    Task Dispatch(UiEvent uiEvent);
}

// The runtime id a handler binds itself with: a plain neuron's own id, or the id behind a proxy brain.Get returned.
public static class UiHandlers
{
    public static string IdOf(DigitalBrain.NeuronId id) => id.ToString();

    public static string IdOf(object neuron)
    {
        ArgumentNullException.ThrowIfNull(neuron);
        return neuron is DigitalBrain.Invocation.NeuronProxy proxy ? proxy.Target.ToString()
            : throw new ArgumentException("A UI handler is a plain neuron, named by its runtime id.", nameof(neuron));
    }
}
