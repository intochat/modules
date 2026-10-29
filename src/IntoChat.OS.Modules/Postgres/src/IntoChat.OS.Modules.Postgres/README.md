# IntoChat.OS.Modules.Postgres

Read-only PostgreSQL queries, schema discovery, and scoped declarative table neurons.

Module id: `postgres`.

How to test: `dotnet test --project src/IntoChat.OS.Modules/Postgres/tests/IntoChat.OS.Modules.Postgres.Tests.Hosting/IntoChat.OS.Modules.Postgres.Tests.Hosting.csproj`

Documentation: https://github.com/intochat/os.docs (modules).

<!-- BEGIN GENERATED options -->
Configuration section: `IntoChatOS:Modules:postgres:Options` (environment prefix `IntoChatOS__Modules__postgres__Options__`).

Options:

None.

Secrets (names only; values are sealed per brain, never configuration):

| Secret | Required |
| --- | --- |
| `AdminConnection` | no |
| `ConnectionString` | yes |

Contracts: assembly `IntoChat.OS.Modules.Postgres.Contracts` (`IPostgresServer`, `ITable`).
Hosting: needs a container.
<!-- END GENERATED -->
