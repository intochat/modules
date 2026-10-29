using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Framework.Modules.Configuration;

namespace IntoChat.OS.Modules.Time;

// Plain timer and reminder neurons in this assembly. The host must supply durable reminders.
[DigitalBrain.Module(TimeModule.ModuleName)]
public sealed class TimeModule : DigitalBrain.Module
{
    internal const string ModuleName = "time";

    public static System.Reflection.Assembly? Contracts => typeof(Reminders.IReminder).Assembly;

    public static readonly string MinimumReminderPeriodKey =
        IntoChatOSConfiguration.ModuleOptionsSection(new ModuleId(ModuleName)) + ":MinimumReminderPeriod";

    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs: [DigitalBrain.Need.Reminders],
        Settings: [DigitalBrain.Setting.Of<TimeModuleOptions>(IntoChatOSConfiguration.ModuleOptionsSection(new ModuleId(ModuleName)))],
        Secrets: []);
}
