using DigitalBrain.Runtime.Orleans;
using DigitalBrain.Spi;
using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter.Preferences.Signals;
using IntoChat.OS.Modules.Flutter.Settings;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Flutter.Preferences;

// One preference a Settings view shows. A brain store is read and written through BrainPreferenceStore, the same
// fields the HTTP preference endpoints use, as the calling person; a Local* store is the device's, so the
// neuron holds only its spec and the renderer reads and writes it on the device.
[Neuron(UIVocabulary.PreferenceType)]
internal sealed class PreferenceControlNeuron
    : UiNeuron<PreferenceControlState>, IPreferenceControl
{
    private const int MaxChoices = 64;

    private INeuronProxies Brain => ServiceProvider.GetRequiredService<INeuronProxies>();
    private IBrainObserver Kernel => ServiceProvider.GetRequiredService<IBrainObserver>();

    private string Scope => NeuronAddress.ScopeOf(FullKey)
        ?? throw new InvalidOperationException("A preference control lives inside a brain.");

    public async Task Configure(PreferenceSpec spec)
    {
        if (!await SettingsCaller.IsShipped(Brain, NeuronAddress.ScopeOf(FullKey)))
        { throw new UnauthorizedAccessException("Only the Settings app configures a preference control."); }
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentException.ThrowIfNullOrWhiteSpace(spec.Label);
        if (spec.Label.Length > 128) { throw new ArgumentException("A preference label is at most 128 characters.", nameof(spec)); }
        if (!Enum.IsDefined(spec.Store)) { throw new ArgumentException("Unknown preference store.", nameof(spec)); }
        if (!Enum.IsDefined(spec.Kind)) { throw new ArgumentException("Unknown preference kind.", nameof(spec)); }
        if (Notification(spec.Store) is not null && (spec.Kind != PreferenceKind.Toggle || spec.Choices is not null))
        { throw new ArgumentException("A notification preference is a toggle with no choices.", nameof(spec)); }
        if (spec.Store == PreferenceStore.NewTabApp && (spec.Kind != PreferenceKind.Choice || spec.Choices is not null))
        { throw new ArgumentException("The new tab preference is a choice of the brain's installed apps.", nameof(spec)); }
        if (spec.Choices is { } choices && (choices.Length > MaxChoices || choices.Any(choice => choice is null
            || string.IsNullOrWhiteSpace(choice.Id) || choice.Id.Length > 128 || string.IsNullOrWhiteSpace(choice.Label)
            || choice.Label.Length > 128 || choice.Detail?.Length > 256)))
        { throw new ArgumentException("Preference choices name an id and a label, at most 64 of them.", nameof(spec)); }
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Spec = spec with { Label = spec.Label.Trim() };
        next.Value = null;
        await Save(next, new PreferenceChanged(spec.Store));
    }

    [DigitalBrain.ReadOnlyCall]
    public async Task<PreferenceControlState> Read()
    {
        var state = new PreferenceControlState { Name = FullKey, Version = Snapshot.Version, Spec = Snapshot.Spec };
        if (state.Spec is not { } spec || IsLocal(spec.Store)) { return state; }
        if (Notification(spec.Store) is { } key)
        {
            state.Value = await BrainPreferenceStore.ReadNotification(Brain, Scope, key, CancellationToken.None) ? "on" : "off";
        }
        else if (spec.Store == PreferenceStore.NewTabApp)
        {
            var choices = await BrainPreferenceStore.NewTabChoices(Brain, Kernel, Scope, CancellationToken.None);
            state.Spec = spec with { Choices = [.. choices.Select(choice => new PreferenceChoice(choice.Name, choice.Title))] };
            state.Value = await BrainPreferenceStore.ReadNewTab(Brain, Scope, choices, CancellationToken.None);
        }
        return state;
    }

    public async Task Set(string value)
    {
        if (BrainPreferenceStore.IsAppCaller()) { throw new UnauthorizedAccessException("Installed apps cannot change a person's preferences."); }
        if (Snapshot.Spec is not { } spec) { throw new InvalidOperationException("The preference control is not configured."); }
        if (IsLocal(spec.Store)) { throw new InvalidOperationException("A device preference is saved on the device, not in the brain."); }
        if (Notification(spec.Store) is { } key)
        {
            var on = value switch { "on" => true, "off" => false, _ => throw new ArgumentException("A notification preference is on or off.", nameof(value)) };
            await BrainPreferenceStore.WriteNotification(Brain, Scope, key, on, CancellationToken.None);
        }
        else if (spec.Store == PreferenceStore.NewTabApp)
        {
            if (!await BrainPreferenceStore.WriteNewTab(Brain, Kernel, Scope, value, CancellationToken.None))
            { throw new ArgumentException("Choose an installed app.", nameof(value)); }
        }
        await Publish(new PreferenceChanged(spec.Store));
    }

    private static bool IsLocal(PreferenceStore store) => (int)store >= 10;

    private static string? Notification(PreferenceStore store) => store switch
    {
        PreferenceStore.NotifyAgentFinished => "agent-finished",
        PreferenceStore.NotifyNeedsApproval => "needs-approval",
        PreferenceStore.NotifyConnectionErrors => "connection-errors",
        PreferenceStore.NotifyProductUpdates => "product-updates",
        _ => null,
    };
}
