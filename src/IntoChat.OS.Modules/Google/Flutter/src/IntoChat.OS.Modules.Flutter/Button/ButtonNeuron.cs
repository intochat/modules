using IntoChat.OS.Runtime;
using DigitalBrain.Spi;
using DigitalBrain;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Button.Signals;
using IntoChat.OS.Modules.Flutter.Settings;
using Microsoft.Extensions.DependencyInjection;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Connections.Platform.Connections;
using DigitalBrain.Runtime.Orleans.Hosting;

namespace IntoChat.OS.Modules.Flutter.Button;

[Neuron(UIVocabulary.ButtonType)]
internal sealed partial class ButtonNeuron
    : UiNeuron<ButtonState>, IButton
{
    public Task Set(string label, string action, bool enabled = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Label = label.Trim();
        next.Action = action.Trim();
        next.Enabled = enabled;
        next.Activation = null;
        return Save(next, new ButtonChanged(FullKey, next.Version));
    }

    public Task SetVisual(ControlVisual visual)
    {
        ControlVisualValidation.Check(visual);
        var next = Snapshot;
        next.Visual = visual;
        next.Version++;
        return Save(next, new ButtonChanged(FullKey, next.Version));
    }

    public async Task SetActivation(string label, string activationJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(activationJson);
        if (activationJson.Length > 32_000) { throw new ArgumentException("Activation JSON has at most 32000 characters.", nameof(activationJson)); }
        string? kind, brainId, connection, connector;
        var brainNamed = false;
        try
        {
            using var document = JsonDocument.Parse(activationJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object) { throw new ArgumentException("Activation must be a JSON object.", nameof(activationJson)); }
            // A repeated key would let a reader other than this check see another value.
            if (document.RootElement.EnumerateObject().GroupBy(property => property.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
            { throw new ArgumentException("Activation keys must be unique.", nameof(activationJson)); }
            kind = Text(document.RootElement, "kind");
            brainId = Text(document.RootElement, "brainId");
            connection = Text(document.RootElement, "connection");
            connector = Text(document.RootElement, "connector");
            brainNamed = document.RootElement.TryGetProperty("brainId", out var named) && named.ValueKind != JsonValueKind.Null;
        }
        catch (JsonException error) { throw new ArgumentException("Activation must be a JSON object.", nameof(activationJson), error); }
        // One click on an install, update or remove runs with the person's session, so only the Apps app the OS
        // ships may set one, and only for the button's own brain: no other app can put a misleading label on it.
        if (kind is not null && AppKinds.Contains(kind))
        {
            var scope = FullKey.Split('/')[0];
            if (brainId != scope || !await SettingsCaller.IsShipped(ServiceProvider.GetRequiredService<INeuronProxies>(), scope, "apps"))
            { throw new UnauthorizedAccessException("Only the Apps app sets an install, update or remove activation."); }
        }
        // Switching the account, reconnecting to a new root, or signing an existing connection in again acts on the
        // person's session in one click: only the shipped apps that own that control may set one, for this brain.
        var own = FullKey.Split('/')[0];
        var owners = SessionOwners(kind);
        var ownBrain = brainId == own || (brainId is null && !brainNamed && string.Equals(kind, "account", StringComparison.OrdinalIgnoreCase));
        var connecting = string.Equals(kind, "connections", StringComparison.OrdinalIgnoreCase);
        if (connecting)
        {
            // The shell signs in the connection id the activation names when it is well formed, else the connector's own id
            // (connect_window.dart): so an existing connection is reached with `connection` absent, empty, not a string or
            // malformed too. A first Connect, for a connector with no connection of that id, stays open to any app.
            var explicitId = ConnectionIdPattern().IsMatch(connection ?? "");
            if (explicitId || await HoldsConnection(own, connector)) { owners = ["settings", "apps"]; }
            // Whatever the app, the sign-in is for this brain only: a brainId that is present must be the button's.
            ownBrain = !brainNamed || brainId == own;
        }
        if (owners is not null)
        {
            var shipped = false;
            var os = ServiceProvider.GetRequiredService<INeuronProxies>();
            foreach (var owner in owners) { shipped = shipped || await SettingsCaller.IsShipped(os, own, owner); }
            if (!ownBrain || !shipped) { throw new UnauthorizedAccessException("Only the app that owns this control sets a session activation."); }
        }
        else if (connecting && !ownBrain)
        { throw new UnauthorizedAccessException("A connection window opens for the button's own brain only."); }
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Label = label.Trim();
        next.Enabled = true;
        next.Action = "activate";
        next.Activation = activationJson;
        await Save(next, new ButtonChanged(next.Name, next.Version));
    }

    private static readonly HashSet<string> AppKinds = new(["install", "update", "remove"], StringComparer.OrdinalIgnoreCase);

    // The shipped apps that may set an account or reconnect activation, or null when the activation is not one.
    private static string[]? SessionOwners(string? kind) => kind?.ToLowerInvariant() switch
    {
        "account" => ["circle"],
        "reconnect" => ["desktop", "workspace"],
        _ => null,
    };

    // The shell's connectWindowConnectionId pattern.
    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,127}$")]
    private static partial Regex ConnectionIdPattern();

    // Whether this brain holds a connection with the id, of any connector (the shell's fallback id is the connector's).
    private async Task<bool> HoldsConnection(string scope, string? id)
    {
        if (string.IsNullOrEmpty(id)) { return false; }
        foreach (var key in await SiloNeurons.Get<IConnectionIndex>(ServiceProvider, IConnectionIndex.Key(scope)).List())
        {
            if (ConnectionKeys.Parse(key).ConnectionId.Equals(id, StringComparison.OrdinalIgnoreCase)) { return true; }
        }
        return false;
    }

    private static string? Text(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public Task ClickCurrent(string expectedAction)
    {
        if (!string.Equals(expectedAction, Snapshot.Action, StringComparison.Ordinal))
        { throw new InvalidOperationException("This button changed. Read it again before clicking."); }
        return Click();
    }

    public async Task Click()
    {
        var next = Snapshot;
        if (!next.Enabled || string.IsNullOrWhiteSpace(next.Action))
        {
            throw new InvalidOperationException("Button is not ready to click.");
        }

        next.Name = FullKey;
        next.Version++;
        next.ClickCount++;
        var clicked = new ButtonClicked(FullKey, next.Action, Guid.NewGuid());
        await Save(next, new ButtonChanged(FullKey, next.Version), clicked);
        await Dispatch(UiEvent.Of(clicked));
    }

    [DigitalBrain.ReadOnlyCall] public Task<ButtonState> Read() => Task.FromResult(Named(Snapshot));

    private ButtonState Named(ButtonState state) { state.Name = FullKey; return state; }
}
