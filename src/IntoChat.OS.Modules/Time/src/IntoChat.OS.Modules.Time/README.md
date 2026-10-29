# IntoChat.OS.Modules.Time

Activation-local timers and persistent periodic reminders with live typed ticks.

Module id: `time`.

How to test: `dotnet test --project src/IntoChat.OS.Modules/Time/tests/IntoChat.OS.Modules.Time.Tests.Unit/IntoChat.OS.Modules.Time.Tests.Unit.csproj`

Documentation: https://github.com/intochat/os.docs (modules).

<!-- BEGIN GENERATED options -->
Configuration section: `IntoChatOS:Modules:time:Options` (environment prefix `IntoChatOS__Modules__time__Options__`).

Options:

| Option | Type | Default | Required |
| --- | --- | --- | --- |
| `MinimumReminderPeriod` | `TimeSpan?` | unset | no |

Secrets (names only; values are sealed per brain, never configuration):

None.

Contracts: assembly `IntoChat.OS.Modules.Time` (`IReminder`, `ITimer`).
Hosting: runs in process.
<!-- END GENERATED -->
