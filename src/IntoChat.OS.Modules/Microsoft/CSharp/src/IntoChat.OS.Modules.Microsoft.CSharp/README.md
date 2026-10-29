# IntoChat.OS.Modules.Microsoft.CSharp

Single-file C# apps that operate neurons, compiled and run in the C# sandbox.

Module id: `csharp`.

How to test: `dotnet test --project src/IntoChat.OS.Modules/Microsoft/CSharp/tests/IntoChat.OS.Modules.Microsoft.CSharp.Tests.E2E/IntoChat.OS.Modules.Microsoft.CSharp.Tests.E2E.csproj`

Documentation: https://github.com/intochat/os.docs (modules).

<!-- BEGIN GENERATED options -->
Configuration section: `IntoChatOS:Modules:csharp:Options` (environment prefix `IntoChatOS__Modules__csharp__Options__`).

Options:

| Option | Type | Default | Required |
| --- | --- | --- | --- |
| `EdgeUrl` | `String` | unset | no |
| `SandboxUrl` | `String` | unset | no |
| `SessionPoolEndpoint` | `String` | unset | no |

Secrets (names only; values are sealed per brain, never configuration):

| Secret | Required |
| --- | --- |
| `RunTokenKey` | yes |

Contracts: assembly `IntoChat.OS.Modules.Microsoft.CSharp.Contracts` (`IBrainPrograms`, `IBrainRuns`, `IBrainRuntime`, `ICSharpFile`).
Hosting: runs in process.
<!-- END GENERATED -->
