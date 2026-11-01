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
}
