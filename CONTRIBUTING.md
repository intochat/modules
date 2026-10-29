# Contributing to `intochat/modules`

Thank you for contributing a module to the IntoChat & DigitalBrain ecosystem!

## 1. Module Structure

Every module follows a clean 3-project DDD structure:

```text
<Module>/
├── Contracts/IntoChat.OS.Modules.<Module>.Contracts/   # [NeuronAlias], [Signal], and DTOs
├── src/IntoChat.OS.Modules.<Module>/                   # [Module("<id>")], ModuleManifest, and Neuron<TState> implementations
└── tests/IntoChat.OS.Modules.<Module>.Tests.Unit/      # ModuleOnTestOSFacts<TModule> + behavioral unit tests on TestOS.Local()
```

## 2. Rules & Invariants

1. **Zero `.Aspire.Hosting` Projects**: Modules never reference `.NET Aspire` hosting packages. Declare HTTP, Webhook, Secret, State, or Container requirements in `ModuleManifest.Needs` (`Need.HttpClient(...)`, `Need.Webhook(...)`, `Need.Container(...)`).
2. **Typed Signals & State**: Every stateful neuron inherits `DigitalBrain.Neuron<TState>` and persists state transitions atomically with a `[Signal]` via `await Save(next, signal)`.
3. **Fast In-Memory Verification**: Every module must include a unit test project inheriting `ModuleOnTestOSFacts<TModule>` that runs in milliseconds on `TestOS.Local()` without Docker.

## 3. Submitting a Pull Request

1. Check the [Trending Module Requests](https://github.com/intochat/modules/issues?q=is%3Aissue+is%3Aopen+label%3Amodule-request+sort%3Areactions-%2B1-desc) or open a new issue using the **Module / Connector Request** template.
2. Scaffold your module with `dotnet new intochat-module -n <Module>`.
3. Verify `dotnet test` passes with zero warnings.
4. Open a PR linking the module request issue.
