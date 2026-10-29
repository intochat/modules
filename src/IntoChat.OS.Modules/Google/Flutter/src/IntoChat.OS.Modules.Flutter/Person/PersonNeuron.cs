using DigitalBrain.Contracts;
using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Person.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Person;

[Neuron(UIVocabulary.PersonType)]
internal sealed class PersonNeuron
    : UiNeuron<PersonState>, IPerson, ISharedPersonReader
{
    public Task Set(string displayName, string avatarUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(avatarUrl);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.DisplayName = displayName.Trim();
        next.AvatarUrl = avatarUrl.Trim();
        return Save(next, new PersonChanged(FullKey, next.Version));
    }

    [DigitalBrain.ReadOnlyCall] public Task<PersonState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}

// Module-only leaf access after the viewer Space neuron authorizes the exact reference.
[DefaultNeuronType(UIVocabulary.PersonType)]
[NeuronAlias("ui.shared-person-reader")]
internal interface ISharedPersonReader
{
    Task<PersonState> Read();
}
