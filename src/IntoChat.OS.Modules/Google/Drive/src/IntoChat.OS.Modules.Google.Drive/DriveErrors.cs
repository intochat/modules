using System.Net;
using System.Text.Json;
using DigitalBrain.Capabilities.Storage;

namespace IntoChat.OS.Modules.Google.Drive;

// Google Drive HTTP failures as typed storage errors (D-16). Messages carry the code and item only: never a token, a header
// or a response body.
internal static class DriveErrors
{
    public static StorageException Map(HttpStatusCode status, string? reason, ItemId? item = null) => (int)status switch
    {
        404 => new NotFound(item),
        401 => new NeedsAuth(item),
        403 when reason is "insufficientPermissions" => new Unsupported(item),
        403 when reason is "storageQuotaExceeded" or "quotaExceeded" => new QuotaExceeded(item),
        403 when reason is "rateLimitExceeded" or "userRateLimitExceeded" or "dailyLimitExceeded" => new Unavailable(item),
        403 => new Denied(item),
        429 => new Unavailable(item),
        >= 500 => new Unavailable(item),
        _ => new Unavailable(item),
    };

    // The first error reason of a Drive error body ({"error":{"errors":[{"reason":"..."}]}}), or null.
    public static string? ReasonOf(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) { return null; }
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0
                && errors[0].TryGetProperty("reason", out var reason) ? reason.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}
