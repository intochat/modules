using IntoChat.OS.Contracts.Compute;

namespace IntoChat.OS.Modules.Google.Auth;

// Shared by the three Google modules: each declares its own operations against this one informational list.
public sealed class GooglePriceList : IPriceList
{
    public GooglePriceList() { }
    public long? MicrosPerUnit(CostKind kind, string item) => null;

    public PriceQuote Quote(CostKind kind, string item, string unit) => new()
    {
        Unit = unit,
        MicrosPerUnit = MicrosPerUnit(kind, item),
        TariffVersion = "google-policy-checked-2026-10-22",
        EffectiveFrom = null,
        Source = item.StartsWith("google.drive.", StringComparison.Ordinal)
            ? "https://developers.google.com/workspace/drive/api/guides/limits"
            : item.StartsWith("google.oauth.", StringComparison.Ordinal)
                ? "https://developers.google.com/identity/protocols/oauth2/web-server"
                : "https://developers.google.com/workspace/gmail/api/reference/quota",
        ChargePolicy = ExternalChargePolicy.Informational,
    };
}
