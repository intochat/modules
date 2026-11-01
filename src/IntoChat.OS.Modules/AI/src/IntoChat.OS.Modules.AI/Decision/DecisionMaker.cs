using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DigitalBrain;
using DigitalBrain.Contracts;
using DigitalBrain.Runtime.Orleans.Hosting;
using IntoChat.OS.Framework;
using IntoChat.OS.Modules.AI.Context;
using IntoChat.OS.Modules.AI.Routing;
using IntoChat.OS.Modules.AI.SystemOne;
using IntoChat.OS.Runtime;
using Microsoft.Extensions.Logging;

namespace IntoChat.OS.Modules.AI.Decision;

public sealed class DecisionMaker(
    IServiceProvider services,
    IExecutionContextProvider contextProvider,
    ILogger<DecisionMaker> log) : IDecisionMaker
{
    public async Task<DecisionResult> DecideAsync(DecisionContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.Intent);

        if (context.Mode == DecisionMode.Deliberate)
        {
            var deliberate = await DecideDeliberateAsync(
                new DeliberateGoal(context.Intent, [.. context.Candidates], context.MaxSteps), ct);
            return new DecisionResult(
                deliberate.Outcome.Answer ?? deliberate.Plan,
                1.0,
                DecisionMode.Deliberate,
                Plan: deliberate.Plan,
                Outcome: deliberate.Outcome);
        }

        var fast = await DecideReactiveAsync(
            new ReactivePrompt(context.Intent, [.. context.Candidates], context.Metadata), ct);

        if (context.Mode == DecisionMode.Reactive || fast.Confidence >= context.ConfidenceThreshold)
        {
            return new DecisionResult(
                fast.Choice,
                fast.Confidence,
                DecisionMode.Reactive,
                fast.Probabilities);
        }

        log.LogInformation(
            "Adaptive decision confidence {Confidence:F2} below threshold {Threshold:F2}; escalating to Deliberate",
            fast.Confidence, context.ConfidenceThreshold);

        var escalated = await DecideDeliberateAsync(
            new DeliberateGoal(context.Intent, [.. context.Candidates], context.MaxSteps), ct);

        return new DecisionResult(
            escalated.Outcome.Answer ?? fast.Choice,
            fast.Confidence,
            DecisionMode.Deliberate,
            fast.Probabilities,
            Plan: escalated.Plan,
            Outcome: escalated.Outcome);
    }

    public async Task<FastDecision> DecideReactiveAsync(ReactivePrompt prompt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        if (prompt.CandidateOperations.Length == 0)
        {
            return new FastDecision("unknown", 0.0, new Dictionary<string, double>(StringComparer.Ordinal));
        }

        var brainId = contextProvider.Current.Caller?.BrainId;

        if (BrainScope.IsValidId(brainId))
        {
            try
            {
                var systemOne = SiloNeurons.Get<ISystemOne>(services, ISystemOne.Key(brainId));
                var criteria = prompt.CandidateOperations.ToDictionary(c => c, _ => (string?)null, StringComparer.Ordinal);
                var question = new SystemOneQuestion.Choice("Which candidate matches this intent?", criteria);
                var result = await systemOne.Ask(new SystemOneRequest(prompt.Intent, new() { ["choice"] = question }), ct);

                if (result.Answers.TryGetValue("choice", out var answer) && answer is SystemOneAnswer.Choice choice)
                {
                    return new FastDecision(choice.Value, choice.Confidence, choice.Probabilities);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "SystemOne evaluation failed; falling back to pattern matching");
            }
        }

        // Fast fallback heuristic matcher for offline / unit test environments
        return FallbackHeuristicMatch(prompt.Intent, prompt.CandidateOperations);
    }

    public async Task<DeliberateDecision> DecideDeliberateAsync(DeliberateGoal goal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(goal);
        var brainId = contextProvider.Current.Caller?.BrainId;

        if (BrainScope.IsValidId(brainId))
        {
            try
            {
                var router = SiloNeurons.Get<IRouter>(services, IRouter.Key(brainId));
                var outcome = await router.Achieve(goal.Objective, goal.AvailableCapabilities, ct);
                return new DeliberateDecision(outcome.Goal, outcome);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Router deliberate decision failed; returning empty outcome");
            }
        }

        var emptyGoal = new GoalRun(goal.Objective, []);
        return new DeliberateDecision(goal.Objective, emptyGoal);
    }

    private static FastDecision FallbackHeuristicMatch(string intent, string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.Equals("app.install", StringComparison.OrdinalIgnoreCase) && intent.Contains("intochat.app/a/", StringComparison.OrdinalIgnoreCase))
            {
                return new FastDecision(candidate, 1.0, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 1.0 });
            }

            if (candidate.Equals("connection.string", StringComparison.OrdinalIgnoreCase) && (intent.Contains("Host=", StringComparison.OrdinalIgnoreCase) || intent.Contains("Server=", StringComparison.OrdinalIgnoreCase) || intent.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)))
            {
                return new FastDecision(candidate, 1.0, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 1.0 });
            }

            if (candidate.Equals("photocraft", StringComparison.OrdinalIgnoreCase) && (intent.Contains("photo", StringComparison.OrdinalIgnoreCase) || intent.Contains("image", StringComparison.OrdinalIgnoreCase) || intent.Contains("background", StringComparison.OrdinalIgnoreCase) || intent.Contains("desaturate", StringComparison.OrdinalIgnoreCase)))
            {
                return new FastDecision(candidate, 0.95, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 0.95 });
            }

            if (candidate.Equals("filmcraft", StringComparison.OrdinalIgnoreCase) && (intent.Contains("film", StringComparison.OrdinalIgnoreCase) || intent.Contains("video", StringComparison.OrdinalIgnoreCase) || intent.Contains("timeline", StringComparison.OrdinalIgnoreCase) || intent.Contains("razor", StringComparison.OrdinalIgnoreCase)))
            {
                return new FastDecision(candidate, 0.95, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 0.95 });
            }

            if (candidate.Equals("settings.navigate", StringComparison.OrdinalIgnoreCase) && (intent.Contains("settings", StringComparison.OrdinalIgnoreCase) || intent.Contains("configure", StringComparison.OrdinalIgnoreCase) || intent.Contains("key", StringComparison.OrdinalIgnoreCase) || intent.Contains("model", StringComparison.OrdinalIgnoreCase) || intent.Contains("ollama", StringComparison.OrdinalIgnoreCase)))
            {
                return new FastDecision(candidate, 0.95, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 0.95 });
            }

            if (candidate.Equals("chart.mutation", StringComparison.OrdinalIgnoreCase) && (intent.Contains("filter", StringComparison.OrdinalIgnoreCase) || intent.Contains("sort", StringComparison.OrdinalIgnoreCase) || (intent.Contains("chart", StringComparison.OrdinalIgnoreCase) && intent.Contains("by", StringComparison.OrdinalIgnoreCase))))
            {
                return new FastDecision(candidate, 0.90, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 0.90 });
            }

            if (candidate.Equals("chart.question", StringComparison.OrdinalIgnoreCase) && (intent.Contains("chart", StringComparison.OrdinalIgnoreCase) || intent.Contains("highest", StringComparison.OrdinalIgnoreCase) || intent.Contains("lowest", StringComparison.OrdinalIgnoreCase)))
            {
                return new FastDecision(candidate, 0.90, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 0.90 });
            }

            if (candidate.Equals("database.query", StringComparison.OrdinalIgnoreCase) && (intent.Contains("select", StringComparison.OrdinalIgnoreCase) || intent.Contains("count", StringComparison.OrdinalIgnoreCase) || intent.Contains("table", StringComparison.OrdinalIgnoreCase) || intent.Contains("query", StringComparison.OrdinalIgnoreCase)))
            {
                return new FastDecision(candidate, 0.90, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 0.90 });
            }

            if (candidate.Equals("file.resource", StringComparison.OrdinalIgnoreCase) && (intent.Contains(".cs", StringComparison.OrdinalIgnoreCase) || intent.Contains(".json", StringComparison.OrdinalIgnoreCase) || intent.Contains("file", StringComparison.OrdinalIgnoreCase)))
            {
                return new FastDecision(candidate, 0.90, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 0.90 });
            }
        }

        var defaultChoice = candidates.Contains("general.chat", StringComparer.OrdinalIgnoreCase)
            ? "general.chat"
            : (candidates.Length > 0 ? candidates[0] : "unknown");
        return new FastDecision(defaultChoice, 0.5, new Dictionary<string, double>(StringComparer.Ordinal) { [defaultChoice] = 0.5 });
    }
}
