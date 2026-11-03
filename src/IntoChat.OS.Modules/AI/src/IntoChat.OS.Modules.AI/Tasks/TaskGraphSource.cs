using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DigitalBrain;
using DigitalBrain.Graph;
using DigitalBrain.Spi;
using Microsoft.Extensions.DependencyInjection;

namespace IntoChat.OS.Modules.AI.Tasks;

public sealed class TaskGraphSource(IServiceProvider services) : IGraphSource
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task Contribute(BrainId brain, GraphSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        var observer = services.GetService<IBrainObserver>();
        var proxies = services.GetService<INeuronProxies>();
        var byId = new Dictionary<string, AgentTask>(StringComparer.Ordinal);

        if (proxies is not null)
        {
            try
            {
                var directory = proxies.Get<ITaskDirectoryNeuron>(new NeuronId(brain, "tasks.directory", "default"));
                var listed = await directory.ListTasksAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                foreach (var task in listed)
                {
                    if (!string.IsNullOrWhiteSpace(task.TaskId))
                    {
                        byId[task.TaskId] = task;
                    }
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // Directory not yet active or inaccessible; fall back to instance scan.
            }
        }

        if (observer is not null)
        {
            var prefix = brain.Value + "/";
            await foreach (var instance in observer.Instances("tasks.task", cancellationToken).ConfigureAwait(false))
            {
                if (!instance.Key.StartsWith(prefix, StringComparison.Ordinal)) { continue; }
                var neuronId = NeuronId.TryParse(instance.Key, out var parsed)
                    ? parsed
                    : new NeuronId(brain, "tasks.task", instance.Key[prefix.Length..]);

                AgentTask? task = null;
                if (proxies is not null)
                {
                    try
                    {
                        task = await proxies.Get<ITaskNeuron>(neuronId).GetAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    {
                        task = null;
                    }
                }

                if (task is null)
                {
                    try
                    {
                        if (await observer.State(neuronId, cancellationToken).ConfigureAwait(false) is { } element)
                        {
                            var state = element.Deserialize<TaskState>(Json);
                            task = state?.ToModel();
                        }
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    {
                        task = null;
                    }
                }

                if (task is not null && !string.IsNullOrWhiteSpace(task.TaskId))
                {
                    byId[task.TaskId] = task;
                }
            }
        }

        if (byId.Count > 0)
        {
            Contribute(brain, sink, [.. byId.Values]);
        }
    }

    public static void Contribute(BrainId brain, GraphSink sink, IReadOnlyList<AgentTask> tasks)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(tasks);

        var root = sink.IndexOf(brain.Value);
        foreach (var task in tasks)
        {
            if (string.IsNullOrWhiteSpace(task.TaskId)) { continue; }
            var taskNodeId = $"{brain.Value}/tasks.task/{task.TaskId}";
            var taskLabel = string.IsNullOrWhiteSpace(task.Title) ? task.TaskId : task.Title;
            var taskNode = sink.Node(taskNodeId, GraphNodeKind.Task, taskLabel);
            if (root >= 0)
            {
                sink.Edge(root, taskNode, GraphEdgeKind.Owns);
            }

            if (!string.IsNullOrWhiteSpace(task.ParentId))
            {
                var parentNodeId = task.ParentId.Contains('/', StringComparison.Ordinal)
                    ? task.ParentId
                    : $"{brain.Value}/tasks.task/{task.ParentId}";
                var parentNode = sink.Node(parentNodeId, GraphNodeKind.Task, task.ParentId);
                sink.Edge(parentNode, taskNode, GraphEdgeKind.Subtask);
            }

            var previousStepNode = -1;
            foreach (var step in (task.Steps ?? []).OrderBy(s => s.Index))
            {
                var stepNodeId = $"{taskNodeId}#step/{step.Index}";
                var stepLabel = string.IsNullOrWhiteSpace(step.Title) ? $"Step {step.Index + 1}" : step.Title;
                var stepNode = sink.Node(stepNodeId, GraphNodeKind.Step, stepLabel);
                sink.Edge(taskNode, stepNode, GraphEdgeKind.Owns);
                if (previousStepNode >= 0)
                {
                    sink.Edge(previousStepNode, stepNode, GraphEdgeKind.Blocks);
                }
                previousStepNode = stepNode;

                if (!string.IsNullOrWhiteSpace(step.Capability))
                {
                    var contractId = step.Capability.StartsWith("contract:", StringComparison.Ordinal)
                        ? step.Capability
                        : "contract:" + step.Capability;
                    var contractLabel = contractId["contract:".Length..];
                    var contractNode = sink.Node(contractId, GraphNodeKind.Contract, contractLabel);
                    sink.Edge(stepNode, contractNode, GraphEdgeKind.Executes);
                }
            }

            foreach (var link in task.Links ?? [])
            {
                if (string.IsNullOrWhiteSpace(link.TargetId)) { continue; }
                var targetId = link.TargetId.Contains('/', StringComparison.Ordinal) || link.TargetId.Contains(':', StringComparison.Ordinal)
                    ? link.TargetId
                    : $"{brain.Value}/tasks.task/{link.TargetId}";
                var targetNode = sink.IndexOf(targetId);
                if (targetNode < 0)
                {
                    var targetKind = targetId.Contains("/apps/", StringComparison.Ordinal)
                        ? GraphNodeKind.App
                        : targetId.Contains("/tasks.task/", StringComparison.Ordinal)
                            ? GraphNodeKind.Task
                            : targetId.StartsWith("fact:", StringComparison.Ordinal)
                                ? GraphNodeKind.Fact
                                : GraphNodeKind.Neuron;
                    targetNode = sink.Node(targetId, targetKind, targetId);
                }

                var edgeKind = (link.Kind ?? string.Empty).Trim().ToLowerInvariant() switch
                {
                    "blocks" => GraphEdgeKind.Blocks,
                    "subtask" => GraphEdgeKind.Subtask,
                    "workson" or "works-on" => GraphEdgeKind.WorksOn,
                    "executes" => GraphEdgeKind.Executes,
                    _ => GraphEdgeKind.Produced,
                };
                sink.Edge(taskNode, targetNode, edgeKind);
            }
        }
    }
}
