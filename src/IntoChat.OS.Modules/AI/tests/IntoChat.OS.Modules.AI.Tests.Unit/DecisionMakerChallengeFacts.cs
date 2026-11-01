using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntoChat.OS.Modules.AI.Context;
using IntoChat.OS.Modules.AI.Decision;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntoChat.OS.Modules.AI.Tests.Unit;

public sealed class DecisionMakerChallengeFacts
{
    private readonly DecisionMaker _decisionMaker;

    public DecisionMakerChallengeFacts()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var contextProvider = new ExecutionContextProvider();
        _decisionMaker = new DecisionMaker(services, contextProvider, NullLogger<DecisionMaker>.Instance);
    }

    [Fact]
    public async Task DecideAsyncThrowsOnNullContextOrWhitespaceIntent()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _decisionMaker.DecideAsync(null!, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _decisionMaker.DecideAsync(new DecisionContext("", ["photocraft"]), TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _decisionMaker.DecideAsync(new DecisionContext("   \t\r\n  ", ["photocraft"]), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DecideAsyncHandlesEmptyCandidatesGracefully()
    {
        var context = new DecisionContext("process image", [], Mode: DecisionMode.Reactive);
        var result = await _decisionMaker.DecideAsync(context, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("unknown", result.Choice);
        Assert.Equal(0.0, result.Confidence);
        Assert.Equal(DecisionMode.Reactive, result.ModeUsed);
    }

    [Fact]
    public async Task DecideAsyncUnderDeliberateModeDirectlyRunsPlan()
    {
        var context = new DecisionContext(
            "Plan migration to microkernel architecture",
            ["plan.architect", "plan.execute"],
            Mode: DecisionMode.Deliberate,
            MaxSteps: 5);

        var result = await _decisionMaker.DecideAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(DecisionMode.Deliberate, result.ModeUsed);
        Assert.Equal(1.0, result.Confidence);
        Assert.NotNull(result.Plan);
        Assert.NotNull(result.Outcome);
        Assert.Equal("Plan migration to microkernel architecture", result.Choice);
    }

    [Fact]
    public async Task DecideAsyncAdaptiveEscalatesWhenUnderThreshold()
    {
        string[] candidates = ["photocraft", "filmcraft"];
        // Unknown intent yields 0.5 confidence in fallback matcher
        var context = new DecisionContext(
            "summarize quarterly balance sheet",
            candidates,
            Mode: DecisionMode.Adaptive,
            ConfidenceThreshold: 0.8);

        var result = await _decisionMaker.DecideAsync(context, TestContext.Current.CancellationToken);

        // Confidence 0.5 is < 0.8, so it must escalate to Deliberate
        Assert.Equal(DecisionMode.Deliberate, result.ModeUsed);
        Assert.NotNull(result.Plan);
        Assert.NotNull(result.Outcome);
    }

    [Fact]
    public async Task DecideAsyncAdaptiveStaysReactiveWhenConfidenceMeetsThreshold()
    {
        string[] candidates = ["photocraft", "filmcraft"];
        // Known photo intent yields 0.95 confidence
        var context = new DecisionContext(
            "remove background from photo.png",
            candidates,
            Mode: DecisionMode.Adaptive,
            ConfidenceThreshold: 0.90);

        var result = await _decisionMaker.DecideAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal("photocraft", result.Choice);
        Assert.Equal(DecisionMode.Reactive, result.ModeUsed);
        Assert.True(result.Confidence >= 0.90);
        Assert.Null(result.Plan);
    }

    [Fact]
    public async Task DecideAsyncHandlesExtremelyLongIntentWithoutFailure()
    {
        var longIntent = "photo " + new string('x', 20000);
        var context = new DecisionContext(longIntent, ["photocraft", "filmcraft"], Mode: DecisionMode.Reactive);

        var result = await _decisionMaker.DecideAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal("photocraft", result.Choice);
        Assert.Equal(DecisionMode.Reactive, result.ModeUsed);
    }

    [Fact]
    public async Task DecideAsyncHandlesUnicodeAndEmojiIntents()
    {
        var emojiIntent = "📸 🖼️ remove photo background with 🎨 sparkles";
        var context = new DecisionContext(emojiIntent, ["photocraft", "filmcraft"], Mode: DecisionMode.Reactive);

        var result = await _decisionMaker.DecideAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal("photocraft", result.Choice);
        Assert.Equal(DecisionMode.Reactive, result.ModeUsed);
    }

    [Fact]
    public async Task DecideDeliberateRespectsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var goal = new DeliberateGoal("Immediate cancel", ["tool1"]);
        var decision = await _decisionMaker.DecideDeliberateAsync(goal, cts.Token);
        Assert.NotNull(decision);
    }
}
