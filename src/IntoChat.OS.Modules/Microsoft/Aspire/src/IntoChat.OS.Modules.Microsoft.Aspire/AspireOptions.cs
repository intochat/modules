using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Microsoft.Aspire;

public sealed class AspireOptions : IModuleOptions
{
    // How long one instance may take to come back ready during a roll before the roll goes back to the previous build.
    public TimeSpan SiloStepBudget { get; set; } = TimeSpan.FromMinutes(3);

    // Retained for configuration compatibility. Membership is now delivered by Orleans'
    // change stream; no deployment timer uses this former polling interval.
    public TimeSpan ClusterSampling { get; set; } = TimeSpan.FromSeconds(5);

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(SiloStepBudget, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ClusterSampling, TimeSpan.Zero);
    }
}
