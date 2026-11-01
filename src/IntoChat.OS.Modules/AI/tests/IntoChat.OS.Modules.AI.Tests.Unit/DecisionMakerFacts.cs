using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IntoChat.OS.Modules.AI.Context;
using IntoChat.OS.Modules.AI.Decision;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntoChat.OS.Modules.AI.Tests.Unit;

public sealed class DecisionMakerFacts
{
    private readonly DecisionMaker _decisionMaker;

    public DecisionMakerFacts()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var contextProvider = new ExecutionContextProvider();
        _decisionMaker = new DecisionMaker(services, contextProvider, NullLogger<DecisionMaker>.Instance);
    }

    [Theory]
    [InlineData("desaturate and remove background from photo.png", "photocraft")]
    [InlineData("filmcraft razor split at 00:04", "filmcraft")]
    [InlineData("open settings and configure model keys", "settings.navigate")]
    [InlineData("filter chart by revenue descending", "chart.mutation")]
    [InlineData("select count(*) from invoices", "database.query")]
    public async Task DecideReactiveClassifiesCommonIntents(string intent, string expectedChoice)
    {
        string[] candidates = ["photocraft", "filmcraft", "settings.navigate", "chart.mutation", "database.query", "general.chat"];
        var context = new DecisionContext(intent, candidates, Mode: DecisionMode.Reactive);

        var result = await _decisionMaker.DecideAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(expectedChoice, result.Choice);
        Assert.Equal(DecisionMode.Reactive, result.ModeUsed);
        Assert.True(result.Confidence >= 0.90);
    }

    [Fact]
    public async Task DecideDeliberateReturnsGoalPlan()
    {
        var goal = new DeliberateGoal("Refactor legacy modules into autonomous agents", ["code.edit", "build.test"]);
        var decision = await _decisionMaker.DecideDeliberateAsync(goal, TestContext.Current.CancellationToken);

        Assert.NotNull(decision);
        Assert.Equal(goal.Objective, decision.Plan);
        Assert.NotNull(decision.Outcome);
    }

    [Fact]
    public async Task DecideAdaptiveEscalatesWhenConfidenceBelowThreshold()
    {
        string[] candidates = ["photocraft", "filmcraft"];
        // Ambiguous intent that won't strongly match photocraft or filmcraft
        var context = new DecisionContext("what is the weather in Oslo today?", candidates, Mode: DecisionMode.Adaptive, ConfidenceThreshold: 0.8);

        var result = await _decisionMaker.DecideAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(DecisionMode.Deliberate, result.ModeUsed);
        Assert.NotNull(result.Plan);
    }
}
