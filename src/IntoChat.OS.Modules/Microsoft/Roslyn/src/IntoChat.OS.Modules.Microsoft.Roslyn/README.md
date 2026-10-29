# IntoChat.OS.Modules.Microsoft.Roslyn

Roslyn workspace neuron. MSBuild loads the solution; the grain keeps it.

Module id: `roslyn`.

How to test: `dotnet test --project src/IntoChat.OS.Modules/Microsoft/Roslyn/tests/IntoChat.OS.Modules.Microsoft.Roslyn.Tests.Unit/IntoChat.OS.Modules.Microsoft.Roslyn.Tests.Unit.csproj`

Documentation: https://github.com/intochat/os.docs (modules).

<!-- BEGIN GENERATED options -->
Configuration section: `IntoChatOS:Modules:roslyn:Options` (environment prefix `IntoChatOS__Modules__roslyn__Options__`).

Options:

None.

Secrets (names only; values are sealed per brain, never configuration):

None.

Contracts: assembly `IntoChat.OS.Modules.Microsoft.Roslyn.Contracts` (`IRoslyn`).
Hosting: runs in process.
<!-- END GENERATED -->
