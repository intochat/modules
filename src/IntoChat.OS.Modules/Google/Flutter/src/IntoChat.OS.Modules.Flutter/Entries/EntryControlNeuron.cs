using DigitalBrain.Spi;
using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter.Entries.Signals;
using IntoChat.OS.Modules.Flutter.Settings;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Connections.Platform.Entries;
using IntoChat.OS.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Flutter.Entries;

[Neuron(UIVocabulary.EntryControlType)]
internal sealed class EntryControlNeuron
    : UiNeuron<EntryControlState>, IEntryControl
{
    public async Task Configure(EntryControlSpec spec)
    {
        if (!await SettingsCaller.IsShipped(ServiceProvider.GetRequiredService<INeuronProxies>(), NeuronAddress.ScopeOf(FullKey)))
        { throw new UnauthorizedAccessException("Only the Settings app configures an entry control."); }
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentException.ThrowIfNullOrWhiteSpace(spec.Label);
        if (spec.Label.Length > 128) { throw new ArgumentException("An entry control label is at most 128 characters.", nameof(spec)); }
        switch (spec.Mode)
        {
            case EntryControlMode.Key when !BrainEntries.KeyedProviderIds.Any(id => string.Equals(spec.EntryName, BrainEntries.ProviderEntry(id), StringComparison.Ordinal)):
                throw new ArgumentException("A key control names provider:{id} for a key-based provider.", nameof(spec));
            case EntryControlMode.Collection when spec.EntryName is not null:
                throw new ArgumentException("A collection control names no entry.", nameof(spec));
            case EntryControlMode.Key or EntryControlMode.Collection:
                break;
            default:
                throw new ArgumentException("Unknown entry control mode.", nameof(spec));
        }
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Spec = spec with { Label = spec.Label.Trim() };
        await Save(next, new EntryControlChanged(next.Name));
    }

    [DigitalBrain.ReadOnlyCall] public Task<EntryControlState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }

    public Task Changed() => Publish(new EntryControlChanged(FullKey));
}
