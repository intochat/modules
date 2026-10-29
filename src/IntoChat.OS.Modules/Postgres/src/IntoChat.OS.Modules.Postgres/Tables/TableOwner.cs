using DigitalBrain.Capabilities;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Postgres.Tables;

// The identity every app table and table index is keyed by: one brain, one installed app.
// The key is the grain key, {brain}/tables/{escaped app key}; it is parsed back wherever a guard
// needs to know whose grain it is.
public readonly record struct TableOwner(string BrainId, string AppId)
{
    private const string Segment = "tables";

    public string Key => $"{BrainId}/{Segment}/{Uri.EscapeDataString(AppId)}";

    public static TableOwner Parse(string key)
    {
        if (key.Split('/') is not [var brain, Segment, var app] || !BrainScope.IsValidId(brain))
        { throw new UnauthorizedAccessException("This storage key does not name a brain and an app."); }
        var owner = new TableOwner(brain, Uri.UnescapeDataString(app));
        if (string.IsNullOrWhiteSpace(owner.AppId) || owner.Key != key)
        { throw new UnauthorizedAccessException("This storage key does not name a brain and an app."); }
        return owner;
    }

    // The owner a stamped caller may act as. Only trusted callers reach table neurons, and a
    // caller is an owner only when the kernel stamped its app id.
    public static TableOwner Current(CapabilityCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!CallerContextStamper.IsTrusted(caller) || string.IsNullOrWhiteSpace(caller.AppId))
        { throw new UnauthorizedAccessException("A trusted brain and app caller is required."); }
        return new(BrainScope.Create(caller.AccountId, caller.BrainId).Id, caller.AppId);
    }

    public static TableOwner Current() => Current(CallerContextStamper.Require());
}
