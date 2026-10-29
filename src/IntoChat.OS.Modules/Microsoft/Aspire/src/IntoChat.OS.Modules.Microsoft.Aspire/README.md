# IntoChat.OS.Modules.Microsoft.Aspire

Aspire AppHost neuron: resource state signals and resource commands over the AppHost bridge.

Module id: `aspire`.

How to test: `dotnet test --project src/IntoChat.OS.Modules/Microsoft/Aspire/tests/IntoChat.OS.Modules.Microsoft.Aspire.Tests.Integration/IntoChat.OS.Modules.Microsoft.Aspire.Tests.Integration.csproj`

Documentation: https://github.com/intochat/os.docs (modules).

<!-- BEGIN GENERATED options -->
Configuration section: `IntoChatOS:Modules:aspire:Options` (environment prefix `IntoChatOS__Modules__aspire__Options__`).

Options:

| Option | Type | Default | Required |
| --- | --- | --- | --- |
| `Fronts` | integration setting | unset | no |
| `Workdir` | integration setting | unset | yes |

Secrets (names only; values are sealed per brain, never configuration):

| Secret | Required |
| --- | --- |
| `CandidateModel` | no |

Contracts: assembly `IntoChat.OS.Modules.Microsoft.Aspire.Contracts` (`ICandidate`, `ICandidates`, `IDeployment`).
Hosting: runs in process.
<!-- END GENERATED -->
