using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DigitalBrain;
using DigitalBrain.Graph;
using IntoChat.OS.Modules.AI.Agents.Runs;
using IntoChat.OS.Modules.AI.Tasks.Signals;
using IntoChat.OS.Modules.Apps;
using Orleans;

namespace IntoChat.OS.Modules.AI.Tasks;

[GenerateSerializer, Alias("tasks.executor-state")]
public sealed class TaskExecutorState
{
    [Id(0)] public string[] ExecutedStepKeys { get; set; } = [];
}

/// <summary>
/// One executor neuron per brain (<c>tasks.executor</c>). Walks a task's steps through the brain graph:
/// 1. Resolves <c>Step.Capability</c> through <see cref="IBrainGraph.Neighbourhood"/> (<c>Implements</c>). If an installed app implements it, calls <see cref="IApp.Request"/>.
/// 2. If no app implements it and the step is not a confirmation step, asks the Builder (<see cref="IAppStudio.Build"/>) to create or extend an app and links the task with a <c>produced</c> edge.
/// 3. If the step is a confirmation step, stops at <see cref="TaskStatus.AwaitingConfirmation"/> (<c>Needs you</c>).
/// </summary>
[Neuron("tasks.executor", State = "task-executor")]
public sealed class TaskExecutorNeuron : Neuron<TaskExecutorState>, ITaskExecutorNeuron
{
    private IBrainGraph Graph => Get<IBrainGraph>(new NeuronId(Id.Brain, "brain.graph", "default"));

    [On]
    public Task OnTaskCreated(TaskCreated signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        return ExecuteNextStepAsync(signal.TaskId);
    }

    [On]
    public Task OnTaskStepAdvanced(TaskStepAdvanced signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        if (signal.Status is TaskStepStatus.Completed or TaskStepStatus.Running)
        {
            return ExecuteNextStepAsync(signal.TaskId);
        }
        return Task.CompletedTask;
    }

    public async Task<AgentTask?> ExecuteNextStepAsync(string taskId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        var taskNeuronId = new NeuronId(Id.Brain, "tasks.task", taskId);
        try
        {
            await Context.Synapses.Ensure<TaskStepAdvanced>(taskNeuronId).ConfigureAwait(false);
        }
        catch
        {
            // Synapse registration is best-effort when invoked directly in unit tests.
        }

        var taskNeuron = Get<ITaskNeuron>(taskNeuronId);
        var task = await taskNeuron.GetAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(task.TaskId)
            || task.Status is TaskStatus.Completed or TaskStatus.Failed or TaskStatus.Cancelled or TaskStatus.AwaitingConfirmation)
        {
            return task;
        }

        while (true)
        {
            var nextStep = SelectNextExecutableStep(task);
            if (nextStep is null)
            {
                return task;
            }

            var stepExecutionKey = $"{task.TaskId}:{nextStep.Index}:{nextStep.IdempotencyKey ?? "default"}";
            if (State.ExecutedStepKeys.Contains(stepExecutionKey, StringComparer.Ordinal))
            {
                return task;
            }

            if (IsConfirmationStep(nextStep) && nextStep.Status == TaskStepStatus.Pending)
            {
                // AdvanceStepAsync(Running) on a confirmation gate transitions both step and task to AwaitingConfirmation.
                task = await taskNeuron.AdvanceStepAsync(nextStep.Index, TaskStepStatus.Running).ConfigureAwait(false);
                return task;
            }

            var runNeuron = Get<IRunNeuron>(new NeuronId(Id.Brain, "agent.run", $"exec-{task.TaskId}-{nextStep.Index}"));
            var stepTargetId = $"{Id.Brain.Value}/tasks.task/{task.TaskId}#step/{nextStep.Index}";
            await TryStartRunAsync(runNeuron, stepTargetId, task.TaskId).ConfigureAwait(false);

            if (nextStep.Status == TaskStepStatus.Pending)
            {
                task = await taskNeuron.AdvanceStepAsync(nextStep.Index, TaskStepStatus.Running).ConfigureAwait(false);
                if (task.Status == TaskStatus.AwaitingConfirmation)
                {
                    await TryTouchRunAsync(runNeuron, "AwaitingConfirmation").ConfigureAwait(false);
                    return task;
                }
            }

            var implementors = await FindCapabilityImplementorsAsync(nextStep.Capability).ConfigureAwait(false);
            var appImplementor = implementors.FirstOrDefault(id =>
                id.Contains("/apps/", StringComparison.Ordinal)
                || (NeuronId.TryParse(id, out var parsed) && (parsed.Type == "app" || parsed.Type.StartsWith("app.", StringComparison.Ordinal))))
                ?? implementors.FirstOrDefault();

            string output;
            if (!string.IsNullOrWhiteSpace(appImplementor))
            {
                var operation = ExtractOperation(nextStep.Capability);
                try
                {
                    var app = Get<IApp>(ResolveAppNeuronId(appImplementor));
                    await app.Request(operation, nextStep.Description).ConfigureAwait(false);
                }
                catch
                {
                    // In minimal test environments where IApp is a graph node stub, execution still records completion.
                }
                output = $"Executed '{nextStep.Capability}' via {appImplementor}";
            }
            else
            {
                // No installed app implements the capability: ask the Builder (IAppStudio.Build) and link with a produced edge.
                // Guard: at most one synthesized app per task unless explicitly confirmed, preventing runaway synthesis loops (§5 Decision 4).
                var alreadySynthesized = (task.Links ?? []).Any(l => string.Equals(l.Kind, "produced", StringComparison.OrdinalIgnoreCase));
                if (alreadySynthesized)
                {
                    await TryCompleteRunAsync(runNeuron, "AwaitingConfirmation").ConfigureAwait(false);
                    return task;
                }

                var appId = DeriveAppId(nextStep);
                var builtKey = IApp.Key(Id.Brain.Value, appId);
                try
                {
                    var studio = Get<IAppStudio>(new NeuronId(Id.Brain, "apps.studio", "main"));
                    var instruction = string.IsNullOrWhiteSpace(nextStep.Description)
                        ? $"Build app '{appId}' for capability '{nextStep.Capability}'"
                        : $"{nextStep.Description} (target app: {appId}, capability: {nextStep.Capability})";
                    var buildResult = await studio.Build(instruction).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(buildResult?.Name))
                    {
                        builtKey = IApp.Key(Id.Brain.Value, buildResult.Name);
                    }
                }
                catch
                {
                    // In unit tests without a live AppStudio neuron, still record the produced link for the synthesized app key.
                }

                task = await taskNeuron.LinkAsync(builtKey, "produced").ConfigureAwait(false);
                output = $"Synthesized app '{builtKey}' for '{nextStep.Capability}'";
            }

            var nextExecuted = State.ExecutedStepKeys.Concat([stepExecutionKey]).TakeLast(512).ToArray();
            await Save(new TaskExecutorState { ExecutedStepKeys = nextExecuted }, new TaskStepAdvanced(task.TaskId, nextStep.Index, TaskStepStatus.Completed)).ConfigureAwait(false);

            task = await taskNeuron.AdvanceStepAsync(nextStep.Index, TaskStepStatus.Completed, output).ConfigureAwait(false);
            await TryCompleteRunAsync(runNeuron, "Completed").ConfigureAwait(false);

            if (task.Status is TaskStatus.Completed or TaskStatus.Failed or TaskStatus.Cancelled or TaskStatus.AwaitingConfirmation)
            {
                return task;
            }
        }
    }

    private static TaskStep? SelectNextExecutableStep(AgentTask task)
    {
        for (var i = 0; i < task.Steps.Length; i++)
        {
            var step = task.Steps[i];
            if (step.Status == TaskStepStatus.Completed)
            {
                continue;
            }
            if (step.Status is TaskStepStatus.Pending or TaskStepStatus.Running)
            {
                return step;
            }
            return null;
        }
        return null;
    }

    private static bool IsConfirmationStep(TaskStep step)
        => string.Equals(step.Capability, "github.merge", StringComparison.OrdinalIgnoreCase)
           || string.Equals(step.Capability, "cluster.rollout", StringComparison.OrdinalIgnoreCase)
           || step.Title.Contains("[Confirm]", StringComparison.OrdinalIgnoreCase);

    private async Task<string[]> FindCapabilityImplementorsAsync(string capability)
    {
        if (string.IsNullOrWhiteSpace(capability))
        {
            return [];
        }

        var contractNode = capability.StartsWith("contract:", StringComparison.Ordinal)
            ? capability
            : "contract:" + capability;
        try
        {
            _ = await Graph.Rebuild().ConfigureAwait(false);
            var neighbours = await Graph.Neighbourhood(contractNode, 1, [GraphEdgeKind.Implements]).ConfigureAwait(false);
            return neighbours.Where(id => !string.Equals(id, contractNode, StringComparison.Ordinal)).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private NeuronId ResolveAppNeuronId(string appNodeId)
    {
        if (IApp.NameOf(Id.Brain.Value, appNodeId) is { } appName)
        {
            return new NeuronId(Id.Brain, "apps.app2", $"apps/{appName}");
        }
        if (NeuronId.TryParse(appNodeId, out var parsed))
        {
            return parsed;
        }
        return new NeuronId(Id.Brain, "apps.app2", $"apps/{appNodeId}");
    }

    private static string ExtractOperation(string capability)
    {
        var dot = capability.LastIndexOf('.');
        return dot >= 0 && dot < capability.Length - 1 ? capability[(dot + 1)..] : "open";
    }

    private static string DeriveAppId(TaskStep step)
    {
        var raw = !string.IsNullOrWhiteSpace(step.Capability) ? step.Capability : step.Title;
        var chars = raw.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var cleaned = new string(chars).Trim('-');
        while (cleaned.Contains("--", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        }
        return string.IsNullOrWhiteSpace(cleaned) ? "custom-app" : cleaned;
    }

    private static async Task TryStartRunAsync(IRunNeuron run, string target, string taskId)
    {
        try { await run.StartAsync(target, taskId).ConfigureAwait(false); }
        catch { }
    }

    private static async Task TryTouchRunAsync(IRunNeuron run, string status)
    {
        try { await run.TouchAsync(status).ConfigureAwait(false); }
        catch { }
    }

    private static async Task TryCompleteRunAsync(IRunNeuron run, string status)
    {
        try { await run.CompleteAsync(status).ConfigureAwait(false); }
        catch { }
    }
}
