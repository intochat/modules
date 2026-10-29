using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Person;

[NeuronAlias("person"), DefaultNeuronType(UIVocabulary.PersonType)]
public interface IPerson
{
    Task Set(string displayName, string avatarUrl);
    [DigitalBrain.ReadOnlyCall] Task<PersonState> Read();
}

[ExportableState]
public sealed class PersonState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string DisplayName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
}
