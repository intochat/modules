using DigitalBrain.Modules.Configuration;
using DigitalBrain.Modules.Options;

namespace IntoChat.OS.Modules.Time;

// Plain timer and reminder neurons in this assembly. The host must supply durable reminders.
[DigitalBrain.Module(TimeModule.ModuleName)]
public sealed class TimeModule : DigitalBrain.Module
{
    internal const string ModuleName = "time";

    public static System.Reflection.Assembly? Contracts => typeof(Reminders.IReminder).Assembly;

    public static readonly string MinimumReminderPeriodKey =
        ModuleConfiguration.ModuleOptionsSection(new ModuleId(ModuleName)) + ":MinimumReminderPeriod";

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [DigitalBrain.Need.Reminders],
        Settings: [DigitalBrain.Setting.Of<TimeModuleOptions>(ModuleConfiguration.ModuleOptionsSection(new ModuleId(ModuleName)))],
        Secrets: []);
}
