using IntoChat.OS.Modules.AI.Routing;

namespace IntoChat.OS.Modules.AI.Tests.Integration;

// What the assistant hands the language model when Jev is unsure: the chosen route first, then the
// next most likely capabilities, so tool choice is anchored without deciding for the model.
public sealed class RouteResultFacts
{
    [Trait("host", "own")]
    [Fact]
    public void HintListsTheChosenRouteThenTheNextLikelyCapabilities()
    {
        var route = new RouteResult("IntoChat.OS.Modules.Time.Timers.ITimer", "Start", 0.62,
            new() { ["timer"] = 0.62, ["apps.app2"] = 0.25, ["ui.text"] = 0.10, ["windows.console"] = 0.03 });

        var hint = route.Hint();

        Assert.StartsWith("Likely capabilities (Jev):\n- IntoChat.OS.Modules.Time.Timers.ITimer.Start (0.62)", hint, StringComparison.Ordinal);
        Assert.Contains("- apps.app2 (0.25)", hint, StringComparison.Ordinal);
        Assert.Contains("- ui.text (0.10)", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("windows.console", hint, StringComparison.Ordinal);
        Assert.DoesNotContain("- timer (", hint, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAppRouteIsDirectWithNoContractToFill()
    {
        var route = new RouteResult(typeof(IntoChat.OS.Modules.Apps.IApp).FullName!, nameof(IntoChat.OS.Modules.Apps.IApp.Request), 0.95, new(), App: "calculator", Operation: "open");

        Assert.True(route.IsDirect);
    }

    [Fact]
    public void AReadOrListIsDirectAndARoutineCallIsNot()
    {
        Assert.True(new RouteResult("X", "Read", 0.9, new()).IsDirect);
        Assert.True(new RouteResult("X", "List", 0.9, new()).IsDirect);
        Assert.False(new RouteResult("X", "Start", 0.9, new()).IsDirect);
    }
}
