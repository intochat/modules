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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntoChat.OS.Modules.AI.Decision;

public sealed class DecisionMaker(
    IServiceProvider services,
    IExecutionContextProvider contextProvider,
    ILogger<DecisionMaker> log) : IDecisionMaker
{
    private const string ChoiceInstructions =
        "Classify the user's intent into the single best matching candidate using the criteria descriptions. Choose 'general.chat' for greetings, capability inquiries ('what can you do?', 'help', 'who are you?'), or general questions that do not explicitly request a tool operation.";

    private static readonly HashSet<string> ConversationalGreetings = new(StringComparer.OrdinalIgnoreCase)
    {
        "hi",
        "hello",
        "hey",
        "hey there",
        "hi there",
        "hello there",
        "yo",
        "sup",
        "howdy",
        "greetings",
        "good morning",
        "good afternoon",
        "good evening",
        "good day",
        "how are you",
        "how are you doing",
        "how's it going",
        "how is it going",
        "what's up",
        "whats up",
        "thanks",
        "thank you",
        "help",
        "help me",
        "ino",
        "hey ino",
        "hi ino",
        "hello ino"
    };

    private static readonly string[] CapabilityInquiryPhrases =
    [
        "what can you do",
        "what do you do",
        "what are you capable of",
        "what are your capabilities",
        "what capabilities do you have",
        "who are you",
        "what are you",
        "tell me about yourself",
        "introduce yourself",
        "what apps do you have",
        "what apps are available",
        "which apps are available",
        "what tools can you use",
        "what tools do you have",
        "what features do you have",
        "how can you help",
        "how do i use you",
        "can you help me"
    ];

    private static readonly string[] SpecializedCandidates =
    [
        "app.install",
        "connection.string",
        "photocraft",
        "filmcraft",
        "settings.navigate",
        "chart.mutation",
        "chart.question",
        "database.query",
        "file.resource",
        "files",
        "tasks"
    ];

    private static readonly char[] TrimPunctuation =
        [' ', '\t', '\r', '\n', '?', '!', '.', ',', ';', ':', '\'', '"', '(', ')', '[', ']', '-', '—'];

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
            new ReactivePrompt(context.Intent, [.. context.Candidates], context.Metadata, context.CandidateDescriptions), ct);

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

        var generalChatCandidate = prompt.CandidateOperations.FirstOrDefault(
            c => c.Equals("general.chat", StringComparison.OrdinalIgnoreCase));
        if (generalChatCandidate is not null && IsConversationalOrCapabilityInquiry(prompt.Intent))
        {
            return new FastDecision(
                generalChatCandidate,
                0.95,
                new Dictionary<string, double>(StringComparer.Ordinal) { [generalChatCandidate] = 0.95 });
        }

        var descriptions = BuildCriteriaDescriptions(
            prompt.CandidateOperations,
            prompt.CandidateDescriptions,
            prompt.Context);

        var brainId = contextProvider.Current.Caller?.BrainId;
        var systemOne = services.GetService<ISystemOne>();

        if (systemOne is not null || BrainScope.IsValidId(brainId))
        {
            try
            {
                systemOne ??= SiloNeurons.Get<ISystemOne>(services, ISystemOne.Key(brainId!));
                var criteria = descriptions.ToDictionary(kv => kv.Key, kv => (string?)kv.Value, StringComparer.Ordinal);
                var question = new SystemOneQuestion.Choice(ChoiceInstructions, criteria);
                var result = await systemOne.Ask(new SystemOneRequest(prompt.Intent, new() { ["choice"] = question }), ct);

                if (result.Answers.TryGetValue("choice", out var answer) && answer is SystemOneAnswer.Choice choice)
                {
                    var matchedCandidate = prompt.CandidateOperations.FirstOrDefault(
                        c => c.Equals(choice.Value, StringComparison.OrdinalIgnoreCase));
                    if (matchedCandidate is not null)
                    {
                        if (generalChatCandidate is not null &&
                            IsSpecializedToolCandidate(matchedCandidate) &&
                            !HasDomainSignal(prompt.Intent, matchedCandidate))
                        {
                            log.LogInformation(
                                "Rejecting ungrounded SystemOne choice '{Choice}' without domain signal; falling back to heuristic match",
                                choice.Value);
                            return FallbackHeuristicMatch(prompt.Intent, prompt.CandidateOperations);
                        }

                        return new FastDecision(matchedCandidate, choice.Confidence, choice.Probabilities);
                    }
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

    public static IReadOnlyDictionary<string, string> BuildCriteriaDescriptions(
        IEnumerable<string> candidates,
        IReadOnlyDictionary<string, string>? explicitDescriptions = null,
        IReadOnlyDictionary<string, string>? context = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var description = TryLookupDescription(candidate, explicitDescriptions)
                ?? TryLookupContextDescription(candidate, context)
                ?? GetBuiltInDescription(candidate);

            result[candidate] = description;
        }

        return result;
    }

    public static bool IsConversationalOrCapabilityInquiry(string intent)
    {
        if (string.IsNullOrWhiteSpace(intent))
        {
            return false;
        }

        if (HasAnySpecializedDomainSignal(intent))
        {
            return false;
        }

        var normalized = intent.Trim(TrimPunctuation);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (ConversationalGreetings.Contains(normalized))
        {
            return true;
        }

        foreach (var phrase in CapabilityInquiryPhrases)
        {
            if (normalized.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasDomainSignal(string intent, string candidate)
    {
        if (string.IsNullOrWhiteSpace(intent) || string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (candidate.Equals("general.chat", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (candidate.Equals("app.install", StringComparison.OrdinalIgnoreCase))
        {
            return ContainsAny(intent, "intochat.app/a/", "install app", "smart app");
        }

        if (candidate.Equals("connection.string", StringComparison.OrdinalIgnoreCase))
        {
            return ContainsAny(intent, "Host=", "Server=", "postgres://", "postgresql://", "connection string");
        }

        if (candidate.Equals("photocraft", StringComparison.OrdinalIgnoreCase))
        {
            return ContainsAny(
                intent,
                "photocraft",
                "photo",
                "image",
                "background",
                "remove background",
                "remove-background",
                "background removal",
                "select subject",
                "select sky",
                "desaturate",
                "grayscale",
                "cutout",
                "gradient",
                "poster",
                "flip",
                "rotate",
                "layer.",
                "image.",
                ".png",
                ".jpg",
                ".jpeg",
                ".webp",
                ".psd");
        }

        if (candidate.Equals("filmcraft", StringComparison.OrdinalIgnoreCase))
        {
            return ContainsAny(
                intent,
                "filmcraft",
                "videocraft",
                "film",
                "video",
                "timeline",
                "razor",
                "split timeline",
                "cut timeline",
                "ripple trim",
                "trim video",
                "trim timeline",
                "lumetri",
                "color grade",
                "export mp4",
                "timeline.",
                "sequence.",
                ".mp4",
                ".mov",
                ".fcproj");
        }

        if (candidate.Equals("settings.navigate", StringComparison.OrdinalIgnoreCase))
        {
            return ContainsAny(
                intent,
                "settings",
                "configure",
                "key",
                "model",
                "ollama",
                "oauth",
                "google drive",
                "github",
                "slack",
                "notion",
                "billing",
                "revoke");
        }

        if (candidate.Equals("chart.mutation", StringComparison.OrdinalIgnoreCase))
        {
            return ContainsAny(intent, "filter", "sort", "pie chart", "line chart", "bar chart", "mutate chart", "filter rows") ||
                   (ContainsAny(intent, "chart", "table") && ContainsAny(intent, "by", "switch", "change", "pie", "line", "bar", "mutate"));
        }

        if (candidate.Equals("chart.question", StringComparison.OrdinalIgnoreCase))
        {
            return ContainsAny(intent, "chart", "highest", "lowest", "top 3", "top 5", "top 10");
        }

        if (candidate.Equals("database.query", StringComparison.OrdinalIgnoreCase))
        {
            var hasSelect = intent.Contains("select", StringComparison.OrdinalIgnoreCase) &&
                            !ContainsAny(intent, "select subject", "select sky");
            return hasSelect ||
                   ContainsAny(intent, "count", "table", "query", "database", "sql", "postgres", "supabase", "opportunities", "invoices");
        }

        if (candidate.Equals("file.resource", StringComparison.OrdinalIgnoreCase) ||
            candidate.Equals("files", StringComparison.OrdinalIgnoreCase))
        {
            return ContainsAny(
                intent,
                "list files",
                "show files",
                "workspace files",
                "files in",
                "read file",
                "inspect file",
                "create file",
                "write file",
                "save file",
                "explain and summarize",
                ".files/",
                "neuron://",
                "file",
                "files",
                "folder",
                "directory",
                ".cs",
                ".json",
                ".md",
                ".txt",
                ".csv",
                ".xml",
                ".yaml",
                ".yml");
        }

        if (candidate.Equals("tasks", StringComparison.OrdinalIgnoreCase))
        {
            return ContainsAny(intent, "task", "tasks", "self-improvement");
        }

        return intent.Contains(candidate, StringComparison.OrdinalIgnoreCase);
    }

    private static FastDecision FallbackHeuristicMatch(string intent, string[] candidates)
    {
        var generalChat = candidates.FirstOrDefault(c => c.Equals("general.chat", StringComparison.OrdinalIgnoreCase));
        if (generalChat is not null && IsConversationalOrCapabilityInquiry(intent))
        {
            return new FastDecision(generalChat, 0.95, new Dictionary<string, double>(StringComparer.Ordinal) { [generalChat] = 0.95 });
        }

        foreach (var candidate in candidates)
        {
            if (candidate.Equals("app.install", StringComparison.OrdinalIgnoreCase) && HasDomainSignal(intent, candidate))
            {
                return new FastDecision(candidate, 1.0, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 1.0 });
            }

            if (candidate.Equals("connection.string", StringComparison.OrdinalIgnoreCase) && HasDomainSignal(intent, candidate))
            {
                return new FastDecision(candidate, 1.0, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 1.0 });
            }

            if (candidate.Equals("photocraft", StringComparison.OrdinalIgnoreCase) && HasDomainSignal(intent, candidate))
            {
                return new FastDecision(candidate, 0.95, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 0.95 });
            }

            if (candidate.Equals("filmcraft", StringComparison.OrdinalIgnoreCase) && HasDomainSignal(intent, candidate))
            {
                return new FastDecision(candidate, 0.95, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 0.95 });
            }

            if (candidate.Equals("settings.navigate", StringComparison.OrdinalIgnoreCase) && HasDomainSignal(intent, candidate))
            {
                return new FastDecision(candidate, 0.95, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 0.95 });
            }

            if (candidate.Equals("chart.mutation", StringComparison.OrdinalIgnoreCase) && HasDomainSignal(intent, candidate))
            {
                return new FastDecision(candidate, 0.90, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 0.90 });
            }

            if (candidate.Equals("chart.question", StringComparison.OrdinalIgnoreCase) && HasDomainSignal(intent, candidate))
            {
                return new FastDecision(candidate, 0.90, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 0.90 });
            }

            if (candidate.Equals("database.query", StringComparison.OrdinalIgnoreCase) && HasDomainSignal(intent, candidate))
            {
                return new FastDecision(candidate, 0.90, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 0.90 });
            }

            if ((candidate.Equals("file.resource", StringComparison.OrdinalIgnoreCase) ||
                 candidate.Equals("files", StringComparison.OrdinalIgnoreCase)) &&
                HasDomainSignal(intent, candidate))
            {
                return new FastDecision(candidate, 0.90, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 0.90 });
            }

            if (candidate.Equals("tasks", StringComparison.OrdinalIgnoreCase) && HasDomainSignal(intent, candidate))
            {
                return new FastDecision(candidate, 0.90, new Dictionary<string, double>(StringComparer.Ordinal) { [candidate] = 0.90 });
            }
        }

        var defaultChoice = generalChat ?? (candidates.Length > 0 ? candidates[0] : "unknown");
        return new FastDecision(defaultChoice, 0.5, new Dictionary<string, double>(StringComparer.Ordinal) { [defaultChoice] = 0.5 });
    }

    private static bool IsSpecializedToolCandidate(string candidate)
    {
        foreach (var specialized in SpecializedCandidates)
        {
            if (candidate.Equals(specialized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnySpecializedDomainSignal(string intent)
    {
        foreach (var specialized in SpecializedCandidates)
        {
            if (HasDomainSignal(intent, specialized))
            {
                return true;
            }
        }

        return false;
    }

    private static string? TryLookupDescription(string candidate, IReadOnlyDictionary<string, string>? descriptions)
    {
        if (descriptions is null || descriptions.Count == 0)
        {
            return null;
        }

        if (descriptions.TryGetValue(candidate, out var direct) && !string.IsNullOrWhiteSpace(direct))
        {
            return direct.Trim();
        }

        foreach (var kv in descriptions)
        {
            if (kv.Key.Equals(candidate, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(kv.Value))
            {
                return kv.Value.Trim();
            }
        }

        return null;
    }

    private static string? TryLookupContextDescription(string candidate, IReadOnlyDictionary<string, string>? context)
    {
        if (context is null || context.Count == 0)
        {
            return null;
        }

        return TryLookupDescription($"desc:{candidate}", context)
            ?? TryLookupDescription(candidate, context);
    }

    private static string GetBuiltInDescription(string candidate) =>
        candidate.Trim().ToLowerInvariant() switch
        {
            "photocraft" =>
                "Photoshop-compatible layered image editor (PhotoCraft). Use ONLY when the user explicitly asks to edit, cutout, remove background, select subject/sky, desaturate, rotate, flip, or create/render an image, photo, poster, or gradient file.",
            "filmcraft" =>
                "Premiere-compatible non-linear video editor (FilmCraft). Use ONLY when the user explicitly asks to edit a video sequence, razor split timeline, ripple trim clips, apply Lumetri color grading, or export MP4 video.",
            "file.resource" or "files" =>
                "Workspace Filesystem (Files VFS / storage.files). Use when the user asks to list workspace files, browse folders, read/inspect/summarize a file, write/create a file, or references a neuron://...files URI.",
            "settings.navigate" =>
                "OS Settings navigation and configuration. Use when the user asks to open settings, configure local Ollama server or AI models, manage OAuth connections (Google Drive, GitHub, Slack, Notion), API keys, variables, billing, team, or revoke/clear settings.",
            "database.query" =>
                "PostgreSQL / Supabase read-only SQL query and data table/chart visualization. Use when the user provides a SELECT query or asks to query/visualize database tables, opportunities, or customer accounts.",
            "chart.mutation" =>
                "Mutate or filter an active UI Chart or Table card (e.g. switch chart to pie/line/bar, or filter rows).",
            "chart.question" =>
                "Answer analytical questions about the currently visible UI Chart or Table (e.g. top 3, highest/lowest values).",
            "connection.string" =>
                "Connect a PostgreSQL database using an explicit Host=/Server=/postgres:// connection string.",
            "app.install" =>
                "Install or review a Smart App from an intochat.app/a/ link.",
            "tasks" =>
                "Create, list, or verify self-improvement agent tasks in the Task Engine.",
            "general.chat" =>
                "General conversational response, greetings ('hey', 'hello', 'good morning'), meta-questions about Ino or system capabilities ('hey, what can you do?', 'help', 'who are you?', 'what apps are available?'), and general Q&A that do not explicitly invoke a specialized tool operation on a target artifact.",
            _ => $"Execute operation '{candidate}' when the user's intent explicitly requests {candidate}."
        };

    private static bool ContainsAny(string text, params ReadOnlySpan<string> needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
