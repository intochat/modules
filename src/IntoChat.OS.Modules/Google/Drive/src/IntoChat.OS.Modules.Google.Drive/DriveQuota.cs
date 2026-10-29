using CultureInfo = System.Globalization.CultureInfo;
using NumberStyles = System.Globalization.NumberStyles;
using DateTimeStyles = System.Globalization.DateTimeStyles;
using System.Text.Json;
using DigitalBrain.Capabilities.Storage;

namespace IntoChat.OS.Modules.Google.Drive;

// The quota of a Google Drive (D-11, CAP-04) from about.get(storageQuota): a limit means Known, no limit means Unlimited, a
// missing storageQuota or anything unparsable means Unknown. Drive's quota is pooled with Gmail and Photos, so Pooled is set.
internal static class DriveQuota
{
    public static Quota From(JsonElement about)
    {
        if (about.ValueKind != JsonValueKind.Object || !about.TryGetProperty("storageQuota", out var quota) || quota.ValueKind != JsonValueKind.Object)
        { return Quota.Unknown; }
        if (!TryLong(quota, "usage", out var usage) || usage < 0) { return Quota.Unknown; }
        if (!quota.TryGetProperty("limit", out var limitElement) || limitElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        { return new Quota(QuotaState.Unlimited, usage, null, Pooled: true); }
        if (!TryLong(quota, "limit", out var limit) || limit <= 0) { return Quota.Unknown; }
        // An account over its quota still reports a known, full quota.
        return Quota.Known(Math.Min(usage, limit), limit, pooled: true);
    }

    public static Quota From(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return From(document.RootElement);
        }
        catch (JsonException) { return Quota.Unknown; }
    }

    // Drive sends int64 values as JSON strings.
    private static bool TryLong(JsonElement owner, string name, out long value)
    {
        value = 0;
        if (!owner.TryGetProperty(name, out var element)) { return false; }
        return element.ValueKind switch
        {
            JsonValueKind.String => long.TryParse(element.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out value),
            JsonValueKind.Number => element.TryGetInt64(out value),
            _ => false,
        };
    }
}
