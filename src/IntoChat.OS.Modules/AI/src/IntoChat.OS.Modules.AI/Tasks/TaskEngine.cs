using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DigitalBrain;
using DigitalBrain.Runtime.Orleans.Hosting;
using IntoChat.OS.Framework;
using IntoChat.OS.Modules.AI.Context;

namespace IntoChat.OS.Modules.AI.Tasks;

public sealed class TaskEngine : ITaskEngine
{
    private readonly Func<string, ITaskNeuron>? _taskResolver;
    private readonly Func<ITaskDirectoryNeuron>? _directoryResolver;
    private readonly IServiceProvider? _services;
    private readonly IExecutionContextProvider? _contextProvider;

    public TaskEngine(IServiceProvider services, IExecutionContextProvider? contextProvider = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _contextProvider = contextProvider;
    }

    public TaskEngine(Func<string, ITaskNeuron> taskResolver, Func<ITaskDirectoryNeuron>? directoryResolver = null)
    {
        ArgumentNullException.ThrowIfNull(taskResolver);
        _taskResolver = taskResolver;
        _directoryResolver = directoryResolver;
    }

    public BrainId ResolveBrainId()
    {
        if (_contextProvider?.Current.Caller?.BrainId is { Length: > 0 } contextBrain)
        {
            return new BrainId(contextBrain);
        }

        if (CallerContextStamper.TryGet(out var stamped) && stamped?.BrainId is { Length: > 0 } stampedBrain)
        {
            return new BrainId(stampedBrain);
        }

        return new BrainId("local");
    }

    private ITaskNeuron GetTaskNeuron(string taskId)
    {
        if (_taskResolver is not null)
        {
            return _taskResolver(taskId);
        }

        if (_services is not null)
        {
            return SiloNeurons.Get<ITaskNeuron>(_services, new NeuronId(ResolveBrainId(), "tasks.task", taskId));
        }

        throw new InvalidOperationException("No task neuron resolver configured.");
    }

    private ITaskDirectoryNeuron GetDirectoryNeuron()
    {
        if (_directoryResolver is not null)
        {
            return _directoryResolver();
        }

        if (_services is not null)
        {
            return SiloNeurons.Get<ITaskDirectoryNeuron>(_services, new NeuronId(ResolveBrainId(), "tasks.directory", "default"));
        }

        throw new InvalidOperationException("No task directory neuron resolver configured.");
    }

    public async Task<AgentTask> CreateTaskAsync(CreateTaskRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        var taskId = $"task-{Guid.NewGuid():N}"[..18];
        var neuron = GetTaskNeuron(taskId);
        return await neuron.InitializeAsync(request).ConfigureAwait(false);
    }

    public async Task<AgentTask?> GetTaskAsync(string taskId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ct.ThrowIfCancellationRequested();
        var neuron = GetTaskNeuron(taskId);
        return await neuron.GetAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AgentTask>> ListTasksAsync(string? owner = null, TaskStatus? status = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var dir = GetDirectoryNeuron();
        return await dir.ListTasksAsync(owner, status).ConfigureAwait(false);
    }

    public async Task<AgentTask> AdvanceTaskStepAsync(string taskId, int stepIndex, TaskStepStatus newStatus, string? output = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ct.ThrowIfCancellationRequested();
        var neuron = GetTaskNeuron(taskId);
        return await neuron.AdvanceStepAsync(stepIndex, newStatus, output).ConfigureAwait(false);
    }

    public async Task<AgentTask> RollbackTaskAsync(string taskId, int toStepIndex, string reason, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ct.ThrowIfCancellationRequested();
        var neuron = GetTaskNeuron(taskId);
        return await neuron.RollbackAsync(toStepIndex, reason).ConfigureAwait(false);
    }

    public async Task<AgentTask> CancelTaskAsync(string taskId, string reason, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ct.ThrowIfCancellationRequested();
        var neuron = GetTaskNeuron(taskId);
        return await neuron.CancelAsync(reason).ConfigureAwait(false);
    }

    public async Task<TaskVerificationResult> VerifyTaskStepAsync(string taskId, int stepIndex, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ct.ThrowIfCancellationRequested();
        var neuron = GetTaskNeuron(taskId);
        return await neuron.VerifyStepAsync(stepIndex).ConfigureAwait(false);
    }

    public async Task<AgentTask> ConfirmTaskStepAsync(string taskId, int stepIndex, string? confirmationToken = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ct.ThrowIfCancellationRequested();
        var neuron = GetTaskNeuron(taskId);
        return await neuron.ConfirmStepAsync(stepIndex, confirmationToken).ConfigureAwait(false);
    }

    public static CreateTaskRequest CreateDevPipelineRequest(string title, string description, string owner, string branch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);

        var steps = new TaskStepDefinition[]
        {
            new("Plan", $"Formulate execution plan for branch {branch}", "ai.plan", null, $"dev-plan-{branch}"),
            new("Edit", $"Execute modifications via Roslyn on branch {branch}", "microsoft.roslyn.edit", null, $"dev-edit-{branch}"),
            new("Test", $"Run test verification suite on branch {branch}", "test.verification", null, $"dev-test-{branch}"),
            new("Candidate", $"Boot candidate app inside sandbox under Aspire for branch {branch}", "aspire.candidates", null, $"dev-cand-{branch}"),
            new("Open PR", $"Push branch and create pull request for branch {branch}", "github.open-pr", null, $"dev-pr-{branch}"),
            new("Await Review", $"Wait for CI checks and reviewer feedback for branch {branch}", "github.await-review", null, $"dev-review-{branch}"),
            new("Merge [Confirm]", $"Merge pull request upon human confirmation for branch {branch}", "github.merge", null, $"dev-merge-{branch}"),
            new("Roll Out [Confirm]", $"Deploy candidate to production cluster upon human confirmation for branch {branch}", "cluster.rollout", null, $"dev-rollout-{branch}"),
        };

        return new CreateTaskRequest(title, description, owner, steps);
    }
}
