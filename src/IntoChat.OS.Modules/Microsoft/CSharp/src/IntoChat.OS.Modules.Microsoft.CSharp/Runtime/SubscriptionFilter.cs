using IntoChat.OS.Modules.Apps.Signals;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// A subscription selector names one AppRequested operation. Any other signal, and a subscription
// with no selector, is delivered whole. Matching uses the signal itself, so the runtime does not
// take a dependency on how a script would have skipped it after the fact.
// A run revision that only contains the subscriber is not delivered: waking it would record another
// run and wake it again. A revision that also contains someone else still is.
internal static class SubscriptionFilter
{

    // The same selector over a plain [Signal] record (06-08: AppRequested is plain), read from its journal JSON.
    public static readonly string RequestedAlias = DigitalBrain.Runtime.Orleans.Signalling.SignalLog.Alias(typeof(AppRequested));
    public static readonly string ActivityAlias = DigitalBrain.Runtime.Orleans.Signalling.SignalLog.Alias(typeof(AppActivity));

    public static bool AcceptsPlain(string? selector, string alias, string? json)
    {
        if (string.IsNullOrEmpty(selector) || alias != RequestedAlias || json is null) { return true; }
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("operation", out var operation)
            && string.Equals(operation.GetString(), selector, StringComparison.Ordinal);
    }

    public static readonly string RunsChangedAlias = DigitalBrain.Runtime.Orleans.Signalling.SignalLog.Alias(typeof(BrainRunsChanged));

    // The self-feed rule over the plain BrainRunsChanged record (06-08), read from its journal JSON: a revision whose behaviors are
    // only the subscriber is not delivered.
    public static bool Delivers(string behavior, string alias, string json)
    {
        if (alias != RunsChangedAlias) { return true; }
        var changed = System.Text.Json.JsonSerializer.Deserialize<BrainRunsChanged>(json, System.Text.Json.JsonSerializerOptions.Web);
        return changed is not { Behaviors: { Length: > 0 } behaviors }
            || behaviors.Any(owner => !string.Equals(owner, behavior, StringComparison.Ordinal));
    }
}
