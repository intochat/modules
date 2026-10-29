# IntoChat.OS.Modules.Supabase

Supabase module: the deployment's Supabase backing store.

Module id: `supabase`.

How to test: `dotnet test --project src/IntoChat.OS.Modules/Supabase/tests/IntoChat.OS.Modules.Supabase.Tests.Hosting/IntoChat.OS.Modules.Supabase.Tests.Hosting.csproj`

Documentation: https://github.com/intochat/os.docs (modules).

<!-- BEGIN GENERATED options -->
Configuration section: `IntoChatOS:Modules:supabase:Options` (environment prefix `IntoChatOS__Modules__supabase__Options__`).

Options:

None.

Secrets (names only; values are sealed per brain, never configuration):

| Secret | Required |
| --- | --- |
| `ConnectionString` | yes |

Contracts: none.
Hosting: needs a container.
<!-- END GENERATED -->
