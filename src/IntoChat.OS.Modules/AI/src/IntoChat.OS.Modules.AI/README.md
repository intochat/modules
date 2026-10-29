# IntoChat.OS.Modules.AI

AI module: multi-provider LLM and embedding clients, agents, and voice transcription.

Module id: `ai`.

How to test: `dotnet test --project src/IntoChat.OS.Modules/AI/tests/IntoChat.OS.Modules.AI.Tests.Hosting/IntoChat.OS.Modules.AI.Tests.Hosting.csproj`

Documentation: https://github.com/intochat/os.docs (modules).

<!-- BEGIN GENERATED options -->
Configuration section: `IntoChatOS:Modules:ai:Options` (environment prefix `IntoChatOS__Modules__ai__Options__`).

Options:

| Option | Type | Default | Required |
| --- | --- | --- | --- |
| `Endpoint` | integration setting | unset | no |
| `Endpoint` | integration setting | unset | no |
| `Endpoint` | integration setting | unset | no |
| `Endpoint` | integration setting | unset | no |
| `Endpoint` | integration setting | unset | no |
| `Endpoint` | integration setting | unset | no |

Secrets (names only; values are sealed per brain, never configuration):

| Secret | Required |
| --- | --- |
| `ApiKey` | yes |
| `ApiKey` | yes |
| `ApiKey` | yes |
| `ApiKey` | yes |
| `ApiKey` | yes |
| `ApiKey` | yes |
| `ApiKey` | yes |

Contracts: assembly `IntoChat.OS.Modules.AI.Contracts` (`IAgentContext`, `IAgent`, `IAiMarker`, `IDeepSeekV41Flash`, `IEmbeddingGemma`, `IEmbeddingModel`, `IEmbedding`, `IFable5`, `IFacts`, `IGemini31Pro`, `IGemini36Flash`, `IGemini36Pro`, `IGeminiEmbedding`, `IGemma4`, `IGpt4oMiniTranscribe`, `IGpt4oTranscribe`, `IGpt54Mini`, `IGpt54Nano`, `IGpt54`, `IGpt56Luna`, `IGpt56Sol`, `IGpt56Terra`, `IGptImage1`, `IGrok46`, `IGroupChat`, `IHaiku45`, `IImageGeneration`, `IImageGenerator`, `IImageModel`, `IIntentUsage`, `IIntents`, `IJev`, `ILLM`, `ILocalJev`, `IModelCatalog`, `IOpus5`, `IQwen35`, `IRouter`, `ISonnet5`, `ISpeechRecognizer`, `ISpeechSynthesizer`, `ISystemOneModel`, `ISystemOne`, `ITextEmbedding3Large`, `ITextEmbedding3Small`, `ITranscription`, `IVectorIndex`, `IWhisper1`, `IWhisperLargeV3Turbo`, `IWhisperSmall`, `IWhisperTiny`).
Hosting: runs in process.
<!-- END GENERATED -->
