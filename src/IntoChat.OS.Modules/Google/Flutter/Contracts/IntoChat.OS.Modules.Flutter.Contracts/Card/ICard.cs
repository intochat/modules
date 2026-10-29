using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Card;

[NeuronAlias("card"), DefaultNeuronType(UIVocabulary.CardType)]
public interface ICard
{
    Task Set(string title, string body, IReadOnlyList<UiChildRef>? children = null);
    [DigitalBrain.ReadOnlyCall] Task<CardState> Read();
}

[ExportableState]
public sealed class CardState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public List<UiChildRef> Children { get; set; } = [];
}
