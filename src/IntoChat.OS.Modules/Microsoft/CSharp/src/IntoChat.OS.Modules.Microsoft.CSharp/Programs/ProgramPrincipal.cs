using DigitalBrain.Capabilities;
using DigitalBrain.Capabilities.Identity;
using DigitalBrain.Runtime.Orleans.Capabilities;
using IntoChat.OS.Framework;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Programs;

// The person a program speaks for (its calls run as that person's app). The ambient stamp when a person or an app deployed it;
// otherwise the brain's owner from the identity directory, asked as the platform. The former fallback, a user stamp made of the
// brain id, names nobody's account and the authority denies every call under it; a platform stamp is not a person either, so a
// program never inherits one.
internal static class ProgramPrincipal
{
    public static async Task<CapabilityCaller?> ForBrain(IServiceProvider services, string brain, CapabilityCaller? held)
    {
        if (CallerContextStamper.TryGet(out var stamped) && IsPerson(stamped, brain)) { return stamped; }
        if (held is { } kept && IsPerson(kept, brain)) { return kept; }
        if (services.GetService<ICapabilityBroker>() is { } broker)
        {
            try
            {
                using var asPlatform = RuntimeCallerContext.Stamp(CapabilityCallers.Platform(brain, brain, brain));
                var directory = await broker.Identity();
                if (await directory.OwnerOfBrain(brain) is { } account) { return CapabilityCallers.Of(account.OwnerPrincipalId, account.AccountId, brain); }
            }
            catch (Exception error) when (error is InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
            {
                // No directory reachable from this host: what was held stays.
            }
        }
        return held;
    }

    // A person's or an app's stamp of this brain; the brain-id user stamp and the platform are not persons.
    internal static bool IsPerson(CapabilityCaller caller, string brain)
        => caller.BrainId == brain && caller.Kind is CallerKind.User or CallerKind.App && caller != CapabilityCallers.Of(brain, brain, brain);
}
