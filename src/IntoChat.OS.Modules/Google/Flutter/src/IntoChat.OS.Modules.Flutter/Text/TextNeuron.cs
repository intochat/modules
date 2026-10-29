using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Text.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Text;

[Neuron(UIVocabulary.TextType)]
internal sealed class TextNeuron
    : UiNeuron<TextState>, IText
{
    public Task Set(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Markdown = markdown;
        return Save(next, new TextChanged(FullKey));
    }

    public async Task<bool> TrySet(string markdown, int expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        if (Snapshot.Version != expectedVersion) { return false; }
        await Set(markdown);
        return true;
    }

    [DigitalBrain.ReadOnlyCall] public Task<TextState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
