# IntoChat.OS.Modules.ClickHouse

ClickHouse module: read-only query neuron, live query tables and hosting registration.

Module id: `clickhouse`.

How to test: `dotnet test --project src/IntoChat.OS.Modules/ClickHouse/tests/IntoChat.OS.Modules.ClickHouse.Tests.Integration/IntoChat.OS.Modules.ClickHouse.Tests.Integration.csproj`

Documentation: https://github.com/intochat/os.docs (modules).

<!-- BEGIN GENERATED options -->
Configuration section: `IntoChatOS:Modules:clickhouse:Options` (environment prefix `IntoChatOS__Modules__clickhouse__Options__`).

Options:

None.

Secrets (names only; values are sealed per brain, never configuration):

| Secret | Required |
| --- | --- |
| `ConnectionString` | yes |

Contracts: assembly `IntoChat.OS.Modules.ClickHouse.Contracts` (`IClickHouse`).
Hosting: needs a container.
<!-- END GENERATED -->
