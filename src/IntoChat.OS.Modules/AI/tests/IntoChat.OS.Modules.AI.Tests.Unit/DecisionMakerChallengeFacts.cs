using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IntoChat.OS.Modules.AI.Context;
using IntoChat.OS.Modules.AI.Decision;
using IntoChat.OS.Modules.AI.SystemOne;
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
        await cts.CancelAsync();

        var goal = new DeliberateGoal("Immediate cancel", ["tool1"]);
        var decision = await _decisionMaker.DecideDeliberateAsync(goal, cts.Token);
        Assert.NotNull(decision);
    }

    [Fact]
    public async Task DecideReactiveRejectsUngroundedSystemOneToolHallucinationAndNeverPicksPhotocraft()
    {
        var stubSystemOne = new StubSystemOne("photocraft", 0.99);
        var services = new ServiceCollection()
            .AddSingleton<ISystemOne>(stubSystemOne)
            .BuildServiceProvider();
        var decisionMaker = new DecisionMaker(services, new ExecutionContextProvider(), NullLogger<DecisionMaker>.Instance);

        string[] candidates = ["photocraft", "filmcraft", "file.resource", "general.chat"];

        // 1. Conversational capability inquiry bypasses ungrounded tool selection deterministically
        var capabilityResult = await decisionMaker.DecideAsync(
            new DecisionContext("hey, what can you do?", candidates, Mode: DecisionMode.Reactive),
            TestContext.Current.CancellationToken);
        Assert.Equal("general.chat", capabilityResult.Choice);
        Assert.True(capabilityResult.Confidence >= 0.90);

        // 2. General conversational question that reaches SystemOne still rejects ungrounded 'photocraft' choice
        var ungroundedResult = await decisionMaker.DecideAsync(
            new DecisionContext("Explain the difference between TCP and UDP", candidates, Mode: DecisionMode.Reactive),
            TestContext.Current.CancellationToken);
        Assert.Equal("general.chat", ungroundedResult.Choice);
        Assert.NotEqual("photocraft", ungroundedResult.Choice);
        Assert.NotNull(stubSystemOne.LastRequest);

        var choiceQuestion = Assert.IsType<SystemOneQuestion.Choice>(stubSystemOne.LastRequest.Questions["choice"]);
        foreach (var candidate in candidates)
        {
            Assert.True(choiceQuestion.Criteria.TryGetValue(candidate, out var description));
            Assert.False(string.IsNullOrWhiteSpace(description));
        }
    }

    [Fact]
    public async Task DecideReactiveForwardsExplicitCandidateDescriptionsToSystemOne()
    {
        var stubSystemOne = new StubSystemOne("photocraft", 0.96);
        var services = new ServiceCollection()
            .AddSingleton<ISystemOne>(stubSystemOne)
            .BuildServiceProvider();
        var decisionMaker = new DecisionMaker(services, new ExecutionContextProvider(), NullLogger<DecisionMaker>.Instance);

        var explicitDescriptions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["photocraft"] = "Explicit PhotoCraft studio description",
            ["general.chat"] = "Explicit conversational fallback description"
        };

        var context = new DecisionContext(
            "remove background from avatar.png",
            ["photocraft", "general.chat"],
            Mode: DecisionMode.Reactive,
            CandidateDescriptions: explicitDescriptions);

        var result = await decisionMaker.DecideAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal("photocraft", result.Choice);
        Assert.Equal(0.96, result.Confidence);
        Assert.NotNull(stubSystemOne.LastRequest);
        var choiceQuestion = Assert.IsType<SystemOneQuestion.Choice>(stubSystemOne.LastRequest.Questions["choice"]);
        Assert.Equal("Explicit PhotoCraft studio description", choiceQuestion.Criteria["photocraft"]);
        Assert.Equal("Explicit conversational fallback description", choiceQuestion.Criteria["general.chat"]);
    }

    private sealed class StubSystemOne(string forcedChoice, double confidence) : ISystemOne
    {
        public SystemOneRequest? LastRequest { get; private set; }

        public Task<SystemOneResult> Ask(SystemOneRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            var probabilities = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [forcedChoice] = confidence
            };
            var answer = new SystemOneAnswer.Choice(forcedChoice, probabilities, confidence);
            return Task.FromResult(new SystemOneResult(
                "stub-systemone",
                new Dictionary<string, SystemOneAnswer>(StringComparer.Ordinal)
                {
                    ["choice"] = answer
                }));
        }
    }
}
