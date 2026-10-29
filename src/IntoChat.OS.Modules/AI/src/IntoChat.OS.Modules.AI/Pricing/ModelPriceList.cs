using System.Text.Json;
using IntoChat.OS.Contracts.Compute;

namespace IntoChat.OS.Modules.AI.Pricing;

// The AI module's own list prices: per concrete model, four token classes priced per million tokens.
// Local models are priced at zero. Micros per million tokens = usd_per_million_tokens * 1_000_000.
internal sealed class ModelPriceList : IPriceList
{
    internal const string ResourceName = "IntoChat.OS.Modules.AI.Pricing.models.prices.json";
    private const decimal MillionTokens = 1_000_000m;
    private static readonly string[] TokenClasses = ["input", "cached", "reasoning", "output"];
    private static readonly Lazy<Dictionary<string, long>> MicrosPerMillion = new(Load);

    public long? MicrosPerUnit(CostKind kind, string item)
    {
        if (kind != CostKind.Model || string.IsNullOrWhiteSpace(item)) { return null; }
        return MicrosPerMillion.Value.TryGetValue(item, out var micros) ? micros : null;
    }

    private static Dictionary<string, long> Load()
    {
        using var stream = typeof(ModelPriceList).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded price list '{ResourceName}' is missing.");
        using var document = JsonDocument.Parse(stream);
        var prices = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var model in document.RootElement.GetProperty("models").EnumerateArray())
        {
            var id = model.GetProperty("id").GetString()!;
            foreach (var tokenClass in TokenClasses)
            {
                prices[$"{id}:{tokenClass}"] = MicrosPerMillionTokens(model, tokenClass);
            }
        }
        return prices;
    }

    private static long MicrosPerMillionTokens(JsonElement model, string tokenClass)
        => model.TryGetProperty(tokenClass, out var rate) && rate.ValueKind is JsonValueKind.Number
            ? (long)(rate.GetDecimal() * MillionTokens)
            : 0L;
}
