using DigitalBrain;
using IntoChat.OS.Modules.Flutter.Button;
using IntoChat.OS.Modules.Flutter.Chat;
using IntoChat.OS.Modules.Flutter.Button.Signals;
using IntoChat.OS.Modules.Flutter.Text;
using IntoChat.OS.Modules.Flutter.Text.Signals;
using IntoChat.OS.Modules.Flutter.TextField;
using IntoChat.OS.Modules.Flutter.TextField.Signals;
using DigitalBrain.Testing;

namespace IntoChat.OS.Modules.Flutter.Tests;

// The plain Flutter controls (06-04), one fact body per behaviour. The Unit tier runs each on TestOS; the Integration tier
// compiles this same file and runs each on TestOS and on SiloHost.Local, so the same fact holds on both runtimes (R4-05).
// These hosts have no legacy UI binding, so a click is journaled and dispatched to no one.
public sealed class ControlNeuronFacts(FlutterFixture fixture) : IClassFixture<FlutterFixture>
{
    private static readonly TimeSpan Within = TimeSpan.FromSeconds(20);

    public static TheoryData<NeuronRuntimeKind> Kinds => FlutterFixture.Kinds;

    [Theory, MemberData(nameof(Kinds))]
    public async Task AClickPublishesTheActionWithAFreshRequestNamedByTheFullKey(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id(UIVocabulary.ButtonType, "click");
        var button = runtime.Get<IButton>(id);

        await button.Set("Open", "navigate:docs");
        await button.Click();
        await button.ClickCurrent("navigate:docs");

        var clicks = await runtime.WaitForSignals<ButtonClicked>(id, 2, Within, ct);
        Assert.All(clicks, click => Assert.Equal($"{id.Brain.Value}/click", click.Name));
        Assert.All(clicks, click => Assert.Equal("navigate:docs", click.Action));
        Assert.NotEqual(clicks[0].RequestId, clicks[1].RequestId);
        Assert.DoesNotContain(clicks, click => click.RequestId == Guid.Empty);
        Assert.Equal(3, (await runtime.Signals<ButtonChanged>(id, ct)).Count);
        var state = await button.Read();
        Assert.Equal(2, state.ClickCount);
        Assert.Equal($"{id.Brain.Value}/click", state.Name);
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task AStaleOrUnreadyButtonRefusesTheClick(NeuronRuntimeKind kind)
    {
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id(UIVocabulary.ButtonType, "guarded");
        var button = runtime.Get<IButton>(id);

        await button.Set("Send", "submit", enabled: false);
        await Assert.ThrowsAsync<InvalidOperationException>(button.Click);
        await button.Set("Send", "submit");
        await Assert.ThrowsAsync<InvalidOperationException>(() => button.ClickCurrent("another"));
        Assert.Equal(0, (await button.Read()).ClickCount);
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task AnActivationMustBeOneJsonObjectWithUniqueKeys(NeuronRuntimeKind kind)
    {
        var runtime = await fixture.Runtime(kind);
        var button = runtime.Get<IButton>(runtime.Id(UIVocabulary.ButtonType, "activation"));

        await Assert.ThrowsAsync<ArgumentException>(() => button.SetActivation("Open", "[]"));
        await Assert.ThrowsAsync<ArgumentException>(() => button.SetActivation("Open", "{\"a\":1,\"a\":2}"));
        await button.SetActivation("Open", "{\"windowId\":\"table-1\"}");

        var state = await button.Read();
        Assert.Equal("activate", state.Action);
        Assert.Equal("{\"windowId\":\"table-1\"}", state.Activation);
        await button.Set("Send", "submit");
        Assert.Null((await button.Read()).Activation);
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task TextKeepsItsMarkdownAndChangesOnlyAtTheVersionItWasRead(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id(UIVocabulary.TextType, "status");
        var text = runtime.Get<IText>(id);

        await text.Set("first");
        var read = await text.Read();
        await text.Set("second");
        Assert.False(await text.TrySet("stale", read.Version));
        Assert.True(await text.TrySet("third", (await text.Read()).Version));

        Assert.Equal("third", (await text.Read()).Markdown);
        Assert.Equal(3, (await runtime.WaitForSignals<TextChanged>(id, 3, Within, ct)).Count);
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task ASecretFieldNeverJournalsItsValue(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id(UIVocabulary.TextFieldType, "secret");
        var field = runtime.Get<ITextField>(id);

        await field.Configure("Password", "secret");
        await field.SetValue("hunter2-set");
        await field.Input("hunter2-input");

        var changes = await runtime.WaitForSignals<TextFieldChanged>(id, 2, Within, ct);
        Assert.All(changes, change => Assert.Equal("", change.Value));
        Assert.DoesNotContain("hunter2", (await field.Read()).Value, StringComparison.Ordinal);
    }
    // 06-05b: the chat index is plain; it titles a default entry from the first message and announces each change.
    [Theory, MemberData(nameof(Kinds))]
    public async Task TheChatIndexTitlesItsFirstMessageAndArchivesByIdOnly(NeuronRuntimeKind kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id(ChatIndex.GrainType, "apps/ino-data/chats");
        var index = runtime.Get<IChatIndex>(id);

        await index.Create("main", ChatIndex.NewChat);
        await index.Touch("main", "  plan   the week  ");
        await index.Archive("main");
        await Assert.ThrowsAsync<ArgumentException>(() => index.Archive("not-an-id"));

        Assert.Equal("plan the week", (await index.Get("main"))!.Title);
        Assert.Empty((await index.List(archived: false)).Entries);
        Assert.Equal("main", Assert.Single((await index.List(archived: true)).Entries).Id);
        var changes = await runtime.WaitForSignals<ChatIndexChanged>(id, 3, Within, ct);
        Assert.All(changes, change => Assert.Equal($"{id.Brain.Value}/apps/ino-data/chats", change.Name));
        Assert.Equal([1L, 2L, 3L], changes.Select(change => change.Revision));
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task ComputeNeuronReturnsDefaultUsageAndRecordsNewUsage(NeuronRuntimeKind kind)
    {
        var runtime = await fixture.Runtime(kind);
        var id = runtime.Id("ui.compute", "usage");
        var compute = runtime.Get<IntoChat.OS.Modules.Flutter.Compute.ICompute>(id);

        var initial = await compute.Read();
        Assert.Equal(1_970_000L, initial.SpentMicros);
        Assert.Equal(3, initial.Rows.Length);

        var updated = await compute.Record("creator", 500_000L);
        Assert.Equal(2_470_000L, updated.SpentMicros);
        Assert.Contains(updated.Rows, r => r.Key == "creator" && r.Micros == 1_180_000L && r.Count == 15L);
    }
}
