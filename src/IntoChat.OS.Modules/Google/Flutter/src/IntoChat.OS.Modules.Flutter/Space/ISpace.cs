using DigitalBrain.Contracts;


namespace IntoChat.OS.Modules.Flutter.Space;

[NeuronAlias("ui.space"), DefaultNeuronType("ui.space")]
// A brain's one space, keyed {brain}/main (R4-18; clean break from the bare brain key).
public interface ISpace
{
    const string Type = "ui.space";

    static string Key(string brainId) => $"{brainId}/main";

    [DigitalBrain.OneWayCall, DigitalBrain.Interleaves] Task Rebuild();
    [DigitalBrain.OneWayCall, DigitalBrain.Interleaves] Task RefreshApp(string key);
    [DigitalBrain.OneWayCall, DigitalBrain.Interleaves] Task Recompose();
    Task<IntoChat.OS.Modules.Flutter.Person.PersonState> ReadProfile(string key);
    Task<IntoChat.OS.Modules.Flutter.Card.CardState> ReadPost(string key);
    Task<IntoChat.OS.Modules.Flutter.Collection.CollectionState> ReadPosts(string key);
}
