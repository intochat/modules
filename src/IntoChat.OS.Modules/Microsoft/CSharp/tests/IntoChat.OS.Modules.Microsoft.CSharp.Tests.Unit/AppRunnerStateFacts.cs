using DigitalBrain;
using IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Unit;

public sealed class AppRunnerStateFacts
{
    [Fact]
    public void RestoreState_RestoresSavedState()
    {
        var app = new CounterApp();

        var restored = AppRunner.RestoreState(app, """{"count":7,"label":"kept"}""");

        Assert.Equal(7, app.State.Count);
        Assert.Equal("kept", app.State.Label);
        Assert.NotNull(restored);
    }

    [Fact]
    public void RestoreState_WithNewPropertyMissing_KeepsDefaults()
    {
        var app = new CounterApp();

        AppRunner.RestoreState(app, """{"count":3}""");

        Assert.Equal(3, app.State.Count);
        Assert.Equal("", app.State.Label);
    }

    [Theory]
    [InlineData("""{"count":"not a number"}""")]
    [InlineData("""not json""")]
    [InlineData("null")]
    public void RestoreState_Unreadable_ThrowsAndLeavesDefaultState(string saved)
    {
        var app = new CounterApp();

        Assert.Throws<AppStateRestoreException>(() => AppRunner.RestoreState(app, saved));
        Assert.Equal(0, app.State.Count);
    }

    [Fact]
    public void RestoreState_Empty_IsANewApp()
    {
        var app = new CounterApp();

        Assert.Null(AppRunner.RestoreState(app, ""));
        Assert.Equal(0, app.State.Count);
    }

    [Fact]
    public void ServedOperations_ServesOperationsTheManifestOmits()
    {
        var definition = new AppDefinition(typeof(CounterApp));

        var served = AppRunner.ServedOperations(definition, ["increment", "missing"]);

        Assert.Equal(["increment", "reset-all"], served);
    }

    [Fact]
    public void ServedOperations_KeepsManifestSpellingAndSkipsReactions()
    {
        var definition = new AppDefinition(typeof(CounterApp));

        var served = AppRunner.ServedOperations(definition, ["Reset-All"]);

        Assert.Equal(["Reset-All", "increment"], served);
        Assert.DoesNotContain(served, name => name.Contains("changed", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Open", "open")]
    [InlineData("HotRestart", "hot-restart")]
    [InlineData("OpenURL", "open-url")]
    public void Kebab_SplitsWords(string method, string expected) => Assert.Equal(expected, AppRunner.Kebab(method));

    public sealed record CounterState(int Count, string Label)
    {
        public CounterState() : this(0, "") { }
    }

    [Signal("tests.counter.changed")]
    public sealed record CounterChanged(int Count) : Signal;

    /*
    # Counter
    Counts; exercises state restore and operation serving.
    */
    [App("counter")]
    public sealed class CounterApp : App<CounterState>
    {
        [On] public void Increment() => Set(State with { Count = State.Count + 1 });

        [On] public void ResetAll() => Set(new CounterState());

        [On("counter/source")]
        public void OnChanged(CounterChanged changed) => Set(State with { Count = changed.Count });
    }
}
