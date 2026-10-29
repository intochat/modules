using DigitalBrain.Testing;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(IntoChat.OS.Modules.Time.Tests.TimeFixture))]

namespace IntoChat.OS.Modules.Time.Tests;

// Integration tier: the Time neurons on TestOS and on SiloHost.Local. The silo accepts reminder periods down to 100 ms
// (test only; production keeps the host default).
public sealed class TimeFixture : BothRuntimesFixture
{
    public static TheoryData<NeuronRuntimeKind> Kinds => NeuronRuntimeKinds.Both;

    protected override IReadOnlyList<Type> Modules { get; } = [typeof(TimeModule)];

    protected override void ConfigureSilo(IServiceCollection services)
        => services.Configure<Orleans.Hosting.ReminderOptions>(options => options.MinimumReminderPeriod = TimeSpan.FromMilliseconds(100));
}
