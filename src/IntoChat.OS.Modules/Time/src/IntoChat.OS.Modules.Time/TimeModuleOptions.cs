using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Time;

public sealed class TimeModuleOptions : IModuleOptions
{
    // Test and local development may accelerate reminders; unset, production keeps the Orleans default.
    [Setting]
    public TimeSpan? MinimumReminderPeriod { get; set; }

    // A period that is not positive is ignored, as an unset one: the Orleans default stands.
    public void Validate() { }
}
