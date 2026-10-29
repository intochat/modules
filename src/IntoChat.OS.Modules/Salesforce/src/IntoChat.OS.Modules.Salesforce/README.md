# IntoChat.OS.Modules.Salesforce

Salesforce module: one org connection neuron, bounded queries and confirmed record writes.

Module id: `salesforce`.

How to test: `dotnet test --project src/IntoChat.OS.Modules/Salesforce/tests/IntoChat.OS.Modules.Salesforce.Tests.Integration/IntoChat.OS.Modules.Salesforce.Tests.Integration.csproj`

Documentation: https://github.com/intochat/os.docs (modules).

<!-- BEGIN GENERATED options -->
Configuration section: `IntoChatOS:Modules:salesforce:Options` (environment prefix `IntoChatOS__Modules__salesforce__Options__`).

Options:

None.

Secrets (names only; values are sealed per brain, never configuration):

| Secret | Required |
| --- | --- |
| `ConsumerKey` | yes |
| `ConsumerSecret` | yes |

Contracts: assembly `IntoChat.OS.Modules.Salesforce.Contracts` (`ISalesforce`).
Hosting: runs in process.
<!-- END GENERATED -->
