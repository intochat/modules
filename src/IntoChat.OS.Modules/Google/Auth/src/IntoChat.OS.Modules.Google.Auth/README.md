# IntoChat.OS.Modules.Google.Auth

Google sign-in shared by the Gmail and Google Drive modules: the `google-oauth` integration and its client secrets, the code exchange and token refresh operations, and the metered Google HTTP client. Gmail and Drive each need this module in the same brain.

Module id: `google-auth`.

How to test: `dotnet test --project src/IntoChat.OS.Modules/Google/Auth/tests/IntoChat.OS.Modules.Google.Auth.Tests.Unit/IntoChat.OS.Modules.Google.Auth.Tests.Unit.csproj`

Documentation: https://github.com/intochat/os.docs (modules).

<!-- BEGIN GENERATED options -->
Configuration section: `IntoChatOS:Modules:google-auth:Options` (environment prefix `IntoChatOS__Modules__google-auth__Options__`).

Options:

None.

Secrets (names only; values are sealed per brain, never configuration):

| Secret | Required |
| --- | --- |
| `ClientId` | yes |
| `ClientSecret` | yes |

Contracts: none.
Hosting: runs in process.
<!-- END GENERATED -->
