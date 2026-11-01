using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntoChat.OS.Modules.AI.Context;
using IntoChat.OS.Modules.AI.Decision;
using IntoChat.OS.Modules.AI.SystemOne;
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
    [InlineData("list files in workspace", "file.resource")]
    [InlineData("read notes.txt", "file.resource")]
    [InlineData("Explain and summarize report.md (neuron://owner/local-disk.files/report.md)", "file.resource")]
    public async Task DecideReactiveClassifiesCommonIntents(string intent, string expectedChoice)
    {
        string[] candidates =
        [
            "photocraft",
            "filmcraft",
            "settings.navigate",
            "chart.mutation",
            "database.query",
            "file.resource",
            "general.chat"
        ];
        var context = new DecisionContext(intent, candidates, Mode: DecisionMode.Reactive);

        var result = await _decisionMaker.DecideAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(expectedChoice, result.Choice);
        Assert.Equal(DecisionMode.Reactive, result.ModeUsed);
        Assert.True(result.Confidence >= 0.90);
    }

    [Theory]
    [InlineData("hey, what can you do?")]
    [InlineData("what can you do?")]
    [InlineData("what can you do")]
    [InlineData("help")]
    [InlineData("who are you?")]
    [InlineData("hello")]
    [InlineData("hi")]
    [InlineData("good morning")]
    [InlineData("what apps do you have?")]
    [InlineData("what tools can you use?")]
    public async Task DecideReactiveClassifiesConversationalAndCapabilityInquiriesAsGeneralChat(string intent)
    {
        string[] candidates =
        [
            "photocraft",
            "filmcraft",
            "settings.navigate",
            "chart.mutation",
            "database.query",
            "file.resource",
            "general.chat"
        ];
        var context = new DecisionContext(intent, candidates, Mode: DecisionMode.Reactive);

        var result = await _decisionMaker.DecideAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal("general.chat", result.Choice);
        Assert.NotEqual("photocraft", result.Choice);
        Assert.Equal(DecisionMode.Reactive, result.ModeUsed);
        Assert.True(result.Confidence >= 0.90);
    }

    [Fact]
    public void BuildCriteriaDescriptionsNeverReturnsNullOrWhitespaceAndHonorsExplicitDescriptions()
    {
        string[] candidates =
        [
            "photocraft",
            "filmcraft",
            "file.resource",
            "settings.navigate",
            "database.query",
            "chart.mutation",
            "chart.question",
            "connection.string",
            "app.install",
            "tasks",
            "general.chat",
            "custom.operation"
        ];
        var explicitDescriptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["custom.operation"] = "Custom domain operation description for testing."
        };

        var descriptions = DecisionMaker.BuildCriteriaDescriptions(candidates, explicitDescriptions);

        Assert.Equal(candidates.Length, descriptions.Count);
        foreach (var candidate in candidates)
        {
            Assert.True(descriptions.TryGetValue(candidate, out var desc));
            Assert.False(string.IsNullOrWhiteSpace(desc));
        }

        Assert.Equal("Custom domain operation description for testing.", descriptions["custom.operation"]);
        Assert.Contains("PhotoCraft", descriptions["photocraft"], StringComparison.Ordinal);
        Assert.Contains("FilmCraft", descriptions["filmcraft"], StringComparison.Ordinal);
        Assert.Contains("general", descriptions["general.chat"], StringComparison.OrdinalIgnoreCase);
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
