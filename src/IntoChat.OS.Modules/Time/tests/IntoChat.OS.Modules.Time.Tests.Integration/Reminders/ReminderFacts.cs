using IntoChat.OS.Modules.Time.Reminders;
using IntoChat.OS.Modules.Time.Reminders.Signals;
using IntoChat.OS.Modules.Time.Tests;
using DigitalBrain.Testing;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Orleans.Runtime;
using DB = DigitalBrain;

namespace IntoChat.OS.Modules.Time.Tests.Integration;

// The plain ReminderNeuron against the silo's reminder provider (06-03): registrations are the runtime host grain's
// (neuron.host/{brain}/reminder/{key}), so provider faults, retries and queued deliveries are observed on that grain id.
[Trait("host", "own")]
public sealed class ReminderFacts(TimeFixture fixture)
{
    private static GrainId HostOf(DB.NeuronId id) => GrainId.Create("neuron.host", id.ToString());

    private async Task<(INeuronRuntime Runtime, IReminder Reminder, GrainId Host)> Start(string key, ReminderControl control, CancellationToken ct)
    {
        var runtime = await fixture.Start(NeuronRuntimeKind.Silo, ct, siloServices: services => services.UseReminderControl(control));
        var id = runtime.Id("reminder", key);
        return (runtime, runtime.Get<IReminder>(id), HostOf(id));
    }

    [Fact]
    public async Task RegistrationUpdatesInPlaceValidatesBeforeMutationAndStopsIdempotently()
    {
        var ct = TestContext.Current.CancellationToken;
        var control = new ReminderControl();
        var (runtime, reminder, host) = await Start("registration", control, ct);
        await using var _ = runtime;
        await reminder.Start(TimeSpan.FromDays(1), TimeSpan.FromMinutes(1));
        var initial = Assert.Single((await control.Table.ReadRows(host, ct)).Reminders);
        foreach (var due in new[] { TimeSpan.FromTicks(-1), Timeout.InfiniteTimeSpan, TimeSpan.MaxValue })
        { await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reminder.Start(due, TimeSpan.FromMinutes(1))); }
        // Under the host's minimum period (100 ms here) the provider refuses before writing.
        foreach (var period in new[] { TimeSpan.Zero, TimeSpan.FromMilliseconds(99), Timeout.InfiniteTimeSpan })
        { await Assert.ThrowsAnyAsync<ArgumentException>(() => reminder.Start(TimeSpan.Zero, period)); }
        Assert.Equal(initial.ETag, Assert.Single((await control.Table.ReadRows(host, ct)).Reminders).ETag);
        await reminder.Start(TimeSpan.FromDays(2), TimeSpan.FromMinutes(2));
        var updated = Assert.Single((await control.Table.ReadRows(host, ct)).Reminders);
        Assert.Equal(initial.ReminderName, updated.ReminderName);
        Assert.Equal(TimeSpan.FromMinutes(2), updated.Period);
        await reminder.Stop();
        await reminder.Stop();
        Assert.Empty((await control.Table.ReadRows(host, ct)).Reminders);
    }

    [Fact]
    public async Task ProviderFailuresReachCallerAndAllowRetry()
    {
        var ct = TestContext.Current.CancellationToken;
        var control = new ReminderControl { FailRegister = true };
        var (runtime, reminder, host) = await Start("failures", control, ct);
        await using var _ = runtime;
        await Assert.ThrowsAsync<IOException>(() => reminder.Start(TimeSpan.FromDays(1), TimeSpan.FromMinutes(1)));
        Assert.Empty((await control.Table.ReadRows(host, ct)).Reminders);
        await reminder.Start(TimeSpan.FromDays(1), TimeSpan.FromMinutes(1));
        control.FailUnregister = true;
        var committed = Assert.Single((await control.Table.ReadRows(host, ct)).Reminders).ETag;
        control.FailRegister = true;
        await Assert.ThrowsAsync<IOException>(() => reminder.Start(TimeSpan.Zero, TimeSpan.FromMinutes(2)));
        Assert.Equal(committed, Assert.Single((await control.Table.ReadRows(host, ct)).Reminders).ETag);
        await Assert.ThrowsAsync<IOException>(reminder.Stop);
        Assert.Single((await control.Table.ReadRows(host, ct)).Reminders);
        await reminder.Stop();
        Assert.Empty((await control.Table.ReadRows(host, ct)).Reminders);
    }

    [Fact]
    public async Task LostAcknowledgementsAndFailedLookupCanBeRetriedWithoutDuplicateRegistrations()
    {
        var ct = TestContext.Current.CancellationToken;
        var control = new ReminderControl { FailAfterRegister = true };
        var (runtime, reminder, host) = await Start("ambiguous", control, ct);
        await using var _ = runtime;
        await Assert.ThrowsAsync<IOException>(() => reminder.Start(TimeSpan.FromDays(1), TimeSpan.FromMinutes(1)));
        Assert.Single((await control.Table.ReadRows(host, ct)).Reminders);
        await reminder.Start(TimeSpan.FromDays(1), TimeSpan.FromMinutes(2));
        Assert.Equal(TimeSpan.FromMinutes(2), Assert.Single((await control.Table.ReadRows(host, ct)).Reminders).Period);
        control.FailRead = true;
        await Assert.ThrowsAsync<IOException>(reminder.Stop);
        Assert.Single((await control.Table.ReadRows(host, ct)).Reminders);
        control.FailAfterUnregister = true;
        await Assert.ThrowsAsync<IOException>(reminder.Stop);
        Assert.Empty((await control.Table.ReadRows(host, ct)).Reminders);
        await reminder.Stop();
    }

    [Fact]
    public async Task QueuedTicksAreAllowedAfterStopAndUnknownNamesAreIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        var control = new ReminderControl();
        var (runtime, reminder, host) = await Start("queued", control, ct);
        await using var _ = runtime;
        var id = runtime.Id("reminder", "queued");
        await reminder.Start(TimeSpan.FromDays(1), TimeSpan.FromMinutes(1));
        await reminder.Stop();
        // Inject already-queued deliveries through Orleans, without invoking the neuron directly.
        await IntoChat.OS.Kernel.Composition.KernelTestHooks.Remind(runtime.Services, host, "unrelated", ct);
        await IntoChat.OS.Kernel.Composition.KernelTestHooks.Remind(runtime.Services, host, "tick", ct);
        Assert.Equal($"{id.Brain.Value}/queued", Assert.Single(await runtime.Signals<ReminderTick>(id, ct)).ReminderId);
        await IntoChat.OS.Kernel.Composition.KernelTestHooks.Remind(runtime.Services, host, "tick", ct);
        Assert.Equal(2, (await runtime.Signals<ReminderTick>(id, ct)).Count);
        Assert.Empty((await control.Table.ReadRows(host, ct)).Reminders);
    }

    [Fact]
    public async Task ReminderReactivatesItsNeuronAfterTheActivationIsCollected()
    {
        var ct = TestContext.Current.CancellationToken;
        var control = new ReminderControl();
        var (runtime, reminder, host) = await Start("reactivate", control, ct);
        await using var _ = runtime;
        await reminder.Start(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
        var before = await control.NextDeliveryAsync(ct);
        await IntoChat.OS.Kernel.Composition.KernelTestHooks.CollectIdleActivations(runtime.Services, ct);
        ReminderDelivery after;
        do { after = await control.NextDeliveryAsync(ct); }
        while (ReferenceEquals(before.Activation, after.Activation));
        Assert.Equal(host, after.Id);
        await reminder.Stop();
    }
}
