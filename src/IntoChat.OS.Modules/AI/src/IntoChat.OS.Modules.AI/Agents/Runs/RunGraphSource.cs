using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DigitalBrain;
using DigitalBrain.Graph;
using DigitalBrain.Spi;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.AI.Agents.Runs;

public sealed class RunGraphSource(IServiceProvider services) : IGraphSource
{
    public const float StuckRunThresholdMinutes = 5f;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task Contribute(BrainId brain, GraphSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        var observer = services.GetService<IBrainObserver>();
        if (observer is null) { return; }

        var proxies = services.GetService<INeuronProxies>();
        var clock = services.GetService<TimeProvider>() ?? TimeProvider.System;
        var prefix = brain.Value + "/";
        var runs = new List<(string Id, RunState State)>();

        await foreach (var instance in observer.Instances("agent.run", cancellationToken).ConfigureAwait(false))
        {
            if (!instance.Key.StartsWith(prefix, StringComparison.Ordinal)) { continue; }
            var id = NeuronId.TryParse(instance.Key, out var parsed)
                ? parsed
                : new NeuronId(brain, "agent.run", instance.Key[prefix.Length..]);

            RunState? state = null;
            if (proxies is not null)
            {
                try
                {
                    state = await proxies.Get<IRunNeuron>(id).ReadAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    state = null;
                }
            }

            if (state is null)
            {
                try
                {
                    if (await observer.State(id, cancellationToken).ConfigureAwait(false) is { } element)
                    {
                        state = element.Deserialize<RunState>(Json);
                    }
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    state = null;
                }
            }

            if (state is not null && !string.IsNullOrWhiteSpace(state.Target))
            {
                runs.Add((id.ToString(), state));
            }
        }

        Contribute(brain, sink, runs, clock.GetUtcNow());
    }

    public static void Contribute(BrainId brain, GraphSink sink, IReadOnlyList<(string Id, RunState State)> runs, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(runs);

        var root = sink.IndexOf(brain.Value);
        foreach (var (id, state) in runs)
        {
            if (string.IsNullOrWhiteSpace(state.Target) && string.IsNullOrWhiteSpace(state.TaskId)) { continue; }

            var label = NeuronId.TryParse(id, out var parsed) ? $"run/{parsed.Key}" : id;
            var runNode = sink.Node(id, GraphNodeKind.Run, label);
            if (root >= 0)
            {
                sink.Edge(root, runNode, GraphEdgeKind.Owns);
            }

            var targetId = ResolveTargetId(brain, state.Target, state.TaskId);
            if (string.IsNullOrWhiteSpace(targetId)) { continue; }

            var targetNode = EnsureTargetNode(sink, targetId);
            if (string.Equals(state.Status, "Running", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state.Status, "AwaitingConfirmation", StringComparison.OrdinalIgnoreCase))
            {
                var elapsedMinutes = state.LastTurn == default
                    ? 0f
                    : (float)Math.Max(0, (now - state.LastTurn).TotalMinutes);
                var weight = elapsedMinutes >= StuckRunThresholdMinutes ? StuckRunThresholdMinutes : 1f;
                sink.Edge(runNode, targetNode, GraphEdgeKind.WorksOn, weight);
            }
            else if (string.Equals(state.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                sink.Edge(runNode, targetNode, GraphEdgeKind.Produced, 1f);
            }
        }
    }

    private static string ResolveTargetId(BrainId brain, string target, string? taskId)
    {
        if (!string.IsNullOrWhiteSpace(target))
        {
            if (target.Contains('/', StringComparison.Ordinal))
            {
                return target;
            }
            return $"{brain.Value}/apps/{target}";
        }

        return !string.IsNullOrWhiteSpace(taskId)
            ? (taskId.Contains('/', StringComparison.Ordinal) ? taskId : $"{brain.Value}/tasks.task/{taskId}")
            : string.Empty;
    }

    private static int EnsureTargetNode(GraphSink sink, string targetId)
    {
        if (sink.IndexOf(targetId) is var existing and >= 0)
        {
            return existing;
        }

        var kind = targetId.Contains("#step/", StringComparison.Ordinal)
            ? GraphNodeKind.Step
            : targetId.Contains("/tasks.task/", StringComparison.Ordinal)
                ? GraphNodeKind.Task
                : targetId.Contains("/apps/", StringComparison.Ordinal)
                    ? GraphNodeKind.App
                    : GraphNodeKind.Neuron;
        return sink.Node(targetId, kind, targetId);
    }
}
