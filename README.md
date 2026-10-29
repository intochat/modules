# IntoChat Modules (`intochat/modules`)

Official and community DDD capability & connector modules for **[DigitalBrain](https://github.com/intochat/digitalbrain)** and **[IntoChat OS](https://github.com/intochat/os)**.

Every module in this repository is a self-contained Bounded Context implementing `DigitalBrain.Module` with a declarative `ModuleManifest` (`Provides`, `Requires`, `Needs`, `Auth`, `Settings`, `Secrets`) and verified in-memory via `TestOS.Local()`.

---

## 🔥 Trending Community Module Requests (Vote with 👍)

Want to connect your DigitalBrain or IntoChat OS to a new service, API, or hardware device?
- **[👉 View Trending Module Requests (Sorted by 👍 Reactions)](https://github.com/intochat/modules/issues?q=is%3Aissue+is%3Aopen+label%3Amodule-request+sort%3Areactions-%2B1-desc)**
- **[✨ Propose a New Module Integration](https://github.com/intochat/modules/issues/new?template=module-request.yml)**

| Requested Module | Category | Proposed Neurons & Signals | Status |
|---|---|---|---|
| **X (Twitter)** (`x`) | Social / Feed | `IXFeed` (`x.feed`), `[Signal("x.post.received")]`, `Need.HttpClient("x-api")`, `Need.Webhook("/webhooks/x", "x.feed")` | 🗳️ Top Requested |
| **Telegram** (`telegram`) | Messaging | `ITelegramBot` (`telegram.bot`), `[Signal("telegram.message.received")]`, `Need.Webhook("/webhooks/telegram", "telegram.bot")` | 🗳️ Voting Open |
| **Linear** (`linear`) | Engineering | `ILinearIssues` (`linear.issues`), `[Signal("linear.issue.updated")]`, `Need.Webhook("/webhooks/linear", "linear.issues")` | 🗳️ Voting Open |
| **Notion** (`notion`) | Knowledge | `INotionPages` (`notion.pages`), `[Signal("notion.page.updated")]`, `Need.HttpClient("notion")` | 🗳️ Voting Open |
| **Slack** (`slack`) | Messaging | `ISlackWorkspace` (`slack.workspace`), `[Signal("slack.message")]`, `Need.Webhook("/webhooks/slack", "slack.workspace")` | 🗳️ Voting Open |
| **Stripe** (`stripe`) | Finance | `IStripeEvents` (`stripe.events`), `[Signal("stripe.invoice.paid")]`, `Need.Webhook("/webhooks/stripe", "stripe.events")` | 🗳️ Voting Open |
| **Home Assistant** (`homeassistant`) | IoT / Smart Home | `IHomeDevices` (`ha.devices`), `[Signal("ha.state.changed")]` | 🗳️ Voting Open |

---

## 📦 Official Modules Catalog

| Module | ID | Kind | Declarative `Needs` / `Auth` | Description |
|---|---|---|---|---|
| **AI & Memory** | `ai` | Capability | `Need.Http`, `Need.Model`, `Need.Secrets` | `MemoryNeuron` (`IMemory`), `AgentContextNeuron` (`IAgentContext`), `RouterNeuron` (Local Jev), `ILLM`, `IVectorIndex`. |
| **Google Gmail** | `gmail` | Connector | `Need.HttpClient("gmail")`, `Need.Webhook("/webhooks/gmail", "gmail.inbox")`, `ModuleAuth("google")` | Reactive inbox neuron emitting `NewEmailReceived` on webhook push + metered Gmail API client. |
| **Google Drive** | `google-drive` | Connector | `Need.HttpClient("google-drive")`, `ModuleAuth("google")` | Google Drive files, folders, and change notifications. |
| **Google Auth** | `google-auth` | Auth | `Need.Http`, `Need.Secrets` | OAuth 2.0 token broker for Google Workspace connectors. |
| **GitHub** | `github` | Connector | `Need.HttpClient("github")`, `Need.Webhook("/webhooks/github", "github.events")`, `ModuleAuth("github")` | Repositories, pull requests, issues, and webhook signal delivery. |
| **Model Context Protocol** | `mcp` | Connector | `Need.HttpClient("mcp")`, `Need.Secrets` | Connects external MCP servers as typed brain capabilities. |
| **Salesforce** | `salesforce` | Connector | `Need.HttpClient("salesforce")`, `ModuleAuth("salesforce")` | CRM objects, SOQL queries, and opportunity tracking. |
| **Aspire Telemetry** | `aspire` | Diagnostics | `Need.State` | `AspireNeuron` (`IAspire`) & `FakeAspire` for reading structured logs, traces, metrics, and resources inside apps and the Creator self-improving loop. |
| **Flame 3D** | `flame` | UI / 3D | `Need.State` | 3D scene graph primitives (`SceneDefinition`, `BrainScene`) rendered by the Flutter shell. |
| **Time** | `time` | Capability | `Need.Reminders` | Durable timers, schedules, and cron reminders. |
| **LocalDisk** | `local-disk` | Storage | `Need.State` | Sandboxed local filesystem access for `local` and `local+cloud` topologies. |
| **Postgres** | `postgres` | Storage | `Need.State`, `Need.Secrets` | Relational storage capability (`storage@1`) backed by PostgreSQL. |
| **Qdrant** | `qdrant` | Vector DB | `Need.Http`, `Need.Secrets` | Distributed vector similarity search for `cloud` topology. |
| **ClickHouse** | `clickhouse` | Analytics | `Need.Http`, `Need.Secrets` | High-throughput analytical event store. |
| **Supabase** | `supabase` | Storage / Auth | `Need.Http`, `Need.Secrets` | Supabase storage and realtime integration. |
| **Playwright** | `microsoft-playwright` | Automation | `Need.Http` | Headless browser automation and web extraction neuron. |
| **Roslyn & C#** | `microsoft-roslyn` / `csharp` | Compiler | `Need.Sandbox` | Contract discovery, syntax analysis, and isolated `csharp-sandbox` execution. |
| **ArtCraft** | `artcraft` | Media | `Need.Http` | Generative visual and canvas assets. |

---

## 🛠️ How to Author a Module

Every module is a pure DDD package with **zero** `.Aspire.Hosting` project required. Simply declare your module's infrastructure needs in `ModuleManifest`, and `IntoChat.OS` provisions metered HTTP pipelines, OAuth tokens, and webhook endpoints automatically:

```csharp
[DigitalBrain.Module("x")]
public sealed class XModule : DigitalBrain.Module
{
    public static DigitalBrain.ModuleManifest Manifest { get; } = new(
        Provides: [],
        Requires: [],
        Needs:
        [
            DigitalBrain.Need.State("IntoChatOS"),
            DigitalBrain.Need.Secrets,
            DigitalBrain.Need.HttpClient("x-api", "IntoChatOS:Modules:x:BaseUrl", requestsPerMinute: 120),
            DigitalBrain.Need.Webhook("/webhooks/x", "x.feed"),
        ],
        Settings: [],
        Secrets: [new("IntoChatOS:Modules:x:ApiKey")])
    {
        Id = "x",
        Auth = new DigitalBrain.ModuleAuth("x-oauth2", ["tweet.read", "users.read"]),
    };
}
```

Then any `App<TState>` in the user's brain can subscribe to your module's live signals with a single attribute:

```csharp
[App("elon-tracker")]
public sealed class ElonTrackerApp : App<ElonTrackerState>
{
    [On("x.feed", Contract = typeof(IXFeed))]
    public void OnPost(XPostReceived post) =>
        Set(State with { Posts = [post, .. State.Posts] });

    public View Render() => new("Elon Tracker",
        new Collection(State.Posts.Select(p => new Entry(p.Id, p.Author, p.Text)).ToArray()));
}
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the 3-folder module layout (`Contracts/`, `src/`, `tests/`) and `ModuleOnTestOSFacts<TModule>` verification guide.
