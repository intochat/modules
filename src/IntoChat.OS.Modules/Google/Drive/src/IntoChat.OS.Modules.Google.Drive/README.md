# IntoChat.OS.Modules.Google.Drive

One Google Drive as storage, signed in through the `google-auth` module's Google OAuth integration. New connections request the `drive` scope for writes; existing `drive.readonly` grants remain read-only until reconnecting. Item permissions further limit writes. Google documents are read as plain text.

Module id: `google-drive`. Needs `google-auth` in the same brain.

How to test: `dotnet test --project src/IntoChat.OS.Modules/Google/Drive/tests/IntoChat.OS.Modules.Google.Drive.Tests.Integration/IntoChat.OS.Modules.Google.Drive.Tests.Integration.csproj`

Documentation: https://github.com/intochat/os.docs (modules).

<!-- BEGIN GENERATED options -->
Configuration section: `IntoChatOS:Modules:google-drive:Options` (environment prefix `IntoChatOS__Modules__google-drive__Options__`).

Options:

None.

Secrets (names only; values are sealed per brain, never configuration):

None.

Contracts: none.
Hosting: runs in process.
<!-- END GENERATED -->
