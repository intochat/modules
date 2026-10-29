using DigitalBrain.Capabilities;
using IntoChat.OS.Framework;
using IntoChat.OS.Runtime;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter;

[ExportableState]
public sealed class UiBindingState
{
    // The handler's runtime id "brain/type/key".
    public string? HandlerId { get; set; }
}

[DigitalBrain.Signal("ui.binding-changed")]
public sealed record UiBindingChanged(string Name) : DigitalBrain.Signal;

// A plain neuron (06-08): a control's binding to the neuron of its own brain that handles its UI events. Bindings stored by the
// grain take the clean break (R4-24): a handler binds again (the circle room and the circle app bind on activation).
[DigitalBrain.Neuron("ui.binding")]
internal sealed class UiBindingNeuron : DigitalBrain.Neuron<UiBindingState>, IUiBinding
{
    private string FullKey => $"{Id.Brain.Value}/{Id.Key}";

    public Task Bind(string handler)
    {
        var id = UiEventDispatch.Parse(handler);
        if (id.Brain != Id.Brain) { throw new UnauthorizedAccessException("A UI binding hands its events to a neuron of its own brain only."); }
        return Save(new() { HandlerId = id.ToString() }, new UiBindingChanged(FullKey));
    }

    public Task Dispatch(UiEvent uiEvent)
    {
        ArgumentNullException.ThrowIfNull(uiEvent);
        if (State.HandlerId is not { } handler) { return Task.CompletedTask; }
        // The caller is the one stamped on this dispatch, never what the sender put in the event.
        return Get<IUiEventHandler>(UiEventDispatch.Parse(handler)).HandleUiEvent(uiEvent with { Caller = UiEventDispatch.Caller() });
    }
}

internal static class UiEventDispatch
{
    public static DigitalBrain.NeuronId Parse(string handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handler);
        try { return DigitalBrain.NeuronId.Parse(handler); }
        catch (FormatException error) { throw new ArgumentException("A handler is named by its runtime id brain/type/key.", nameof(handler), error); }
    }

    // The caller stamped at the trusted edge this dispatch descends from, or null (a background dispatch).
    public static CapabilityCaller? Caller()
        => CallerContextStamper.TryGet(out var caller) ? caller : null;
}
