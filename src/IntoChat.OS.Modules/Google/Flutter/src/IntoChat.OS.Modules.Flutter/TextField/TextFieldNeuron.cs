using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.TextField.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.TextField;

[Neuron(UIVocabulary.TextFieldType)]
internal sealed class TextFieldNeuron
    : UiNeuron<TextFieldState>, ITextField
{
    private static readonly HashSet<string> Kinds = new(StringComparer.OrdinalIgnoreCase) { "text", "search", "number", "password", "secret", "suggest", "multiline" };
    private static readonly HashSet<string> SecretKinds = new(StringComparer.OrdinalIgnoreCase) { "password", "secret" };

    // Secret kinds never persist or journal the value: state records only that a value is set.
    internal const string SecretSetMarker = "••••••••";

    private static bool IsSecret(string kind) => SecretKinds.Contains(kind);

    private static string Stored(string kind, string value) => !IsSecret(kind) ? value : value.Length == 0 ? "" : SecretSetMarker;

    // A field not yet configured may become a secret, so its value is not journalled either.
    private static string Journalled(TextFieldState state, string value) => IsSecret(state.Kind) || state.Label.Length == 0 ? "" : value;

    public Task Configure(string label, string kind, string? submitButton = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (submitButton is not null && (string.IsNullOrWhiteSpace(submitButton) || submitButton.Length > 512))
        { throw new ArgumentException("A submit button name must contain between 1 and 512 characters.", nameof(submitButton)); }
        if (!Kinds.Contains(kind)) { throw new ArgumentOutOfRangeException(nameof(kind)); }
        var next = Snapshot;
        var wasSecret = IsSecret(next.Kind);
        next.Name = FullKey;
        next.Version++;
        next.Label = label.Trim();
        next.Kind = kind.Trim().ToLowerInvariant();
        next.SubmitButton = submitButton;
        if (IsSecret(next.Kind) && next.Value.Length > 0) { next.Value = SecretSetMarker; }
        // The marker is not a value: a secret that becomes plain text starts empty.
        else if (wasSecret && !IsSecret(next.Kind)) { next.Value = ""; }
        return Save(next, new TextFieldChanged(FullKey, Journalled(next, next.Value)));
    }

    public Task SetValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Value = Stored(next.Kind, value);
        return Save(next, new TextFieldChanged(FullKey, Journalled(next, value)));
    }

    public async Task<bool> TrySetValue(string value, int expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (Snapshot.Version != expectedVersion) { return false; }
        await SetValue(value);
        return true;
    }

    public async Task Input(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 32_000) { throw new ArgumentException("Input has at most 32000 characters.", nameof(value)); }
        await SetValue(value);
        // The binding is the only recipient of a secret value; it forwards without journalling.
        await Dispatch(UiEvent.Of(new TextFieldChanged(FullKey, value)));
    }

    [DigitalBrain.ReadOnlyCall] public Task<TextFieldState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
