# IntoChat.OS.Modules.Microsoft.Windows

A Windows PC as a connection: its files and console, run only on that machine and inside allowed roots.

Module id: `windows`.

Storage writes stay inside the current allowed roots. Delete moves items into `.intochat-trash` under their allowed root; each receipt folder keeps the original item and a `receipt.json` recording its original path. Recover an item with Windows File Explorer by moving it back to that path. Only an explicitly permanent delete removes it immediately. Storage listings and search hide trash and unfinished uploads; symbolic links and junctions are refused.

How to test: `dotnet test --project src/IntoChat.OS.Modules/Microsoft/Windows/tests/IntoChat.OS.Modules.Microsoft.Windows.Tests.Integration/IntoChat.OS.Modules.Microsoft.Windows.Tests.Integration.csproj`

Documentation: https://github.com/intochat/os.docs (modules).

<!-- BEGIN GENERATED options -->
Configuration section: `IntoChatOS:Modules:windows:Options` (environment prefix `IntoChatOS__Modules__windows__Options__`).

Options:

None.

Secrets (names only; values are sealed per brain, never configuration):

None.

Contracts: assembly `IntoChat.OS.Modules.Microsoft.Windows` (`IConsole`, `IFileSystem`, `IWindows`).
Hosting: runs in process.
<!-- END GENERATED -->
