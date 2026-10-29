using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Color.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Color;

[Neuron(UIVocabulary.ColorType)]
internal sealed class ColorNeuron
    : UiNeuron<ColorState>, IColor
{
    private static readonly Regex Hex = new("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public Task Set(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);
        if (!Hex.IsMatch(hex)) { throw new ArgumentException("Color must be #RRGGBB.", nameof(hex)); }
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Hex = hex.ToUpperInvariant();
        return Save(next, new ColorChanged(FullKey, next.Hex));
    }

    [DigitalBrain.ReadOnlyCall] public Task<ColorState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
