using DigitalBrain.Contracts;
using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Card.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Card;

[Neuron(UIVocabulary.CardType)]
internal sealed class CardNeuron
    : UiNeuron<CardState>, ICard, ISharedCardReader
{
    public Task Set(string title, string body, IReadOnlyList<UiChildRef>? children = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(body);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Title = title.Trim();
        next.Body = body;
        next.Children = [.. children ?? []];
        return Save(next, new CardChanged(FullKey, next.Version));
    }

    [DigitalBrain.ReadOnlyCall] public Task<CardState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}

// Module-only leaf access after the viewer Space neuron authorizes the exact reference.
[DefaultNeuronType(UIVocabulary.CardType)]
[NeuronAlias("ui.shared-card-reader")]
internal interface ISharedCardReader
{
    Task<CardState> Read();
}
