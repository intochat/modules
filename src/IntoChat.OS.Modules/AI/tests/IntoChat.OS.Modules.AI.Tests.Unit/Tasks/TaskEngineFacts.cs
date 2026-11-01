using System;
using System.Threading.Tasks;
using DigitalBrain;
using DigitalBrain.Capabilities;
using DigitalBrain.Testing;
using IntoChat.OS.Framework;
using IntoChat.OS.Modules.AI.Context;
using IntoChat.OS.Modules.AI.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TaskStatus = IntoChat.OS.Modules.AI.Tasks.TaskStatus;

namespace IntoChat.OS.Modules.AI.Tests.Unit.Tasks;

public sealed class TaskEngineFacts
{
    private static (TestOS os, TaskEngine engine) CreateHarness()
    {
        var os = TestOS.Local();
        os.Register<TaskNeuron>();
        os.Register<TaskDirectoryGrain>();

        var engine = new TaskEngine(
            taskId => os.Get<ITaskNeuron>(new NeuronId(new BrainId("local"), "tasks.task", taskId)),
            () => os.Get<ITaskDirectoryNeuron>(new NeuronId(new BrainId("local"), "tasks.directory", "default")));

        return (os, engine);
    }

    [Fact]
    public async Task CreateTaskAsync_InitializesTaskWithStepsAndDirectoryRegistration()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine) = CreateHarness();
        await using (os)
        {
            var req = new CreateTaskRequest(
                "Image Pipeline",
                "Process user upload",
                "alice",
                [
                    new TaskStepDefinition("Download", "Fetch asset", "http.get"),
                    new TaskStepDefinition("Resize", "1080p transform", "image.scale")
                ]);

            var created = await engine.CreateTaskAsync(req, ct);

            Assert.False(string.IsNullOrWhiteSpace(created.TaskId));
            Assert.Equal("Image Pipeline", created.Title);
            Assert.Equal("alice", created.Owner);
            Assert.Equal(TaskStatus.Pending, created.Status);
            Assert.Equal(2, created.Steps.Length);
            Assert.Equal(TaskStepStatus.Pending, created.Steps[0].Status);
            Assert.Equal(TaskStepStatus.Pending, created.Steps[1].Status);

            var fetched = await engine.GetTaskAsync(created.TaskId, ct);
            Assert.NotNull(fetched);
            Assert.Equal(created.TaskId, fetched.TaskId);
            Assert.Equal(created.Title, fetched.Title);

            var tasks = await engine.ListTasksAsync("alice", ct: ct);
            Assert.Contains(tasks, t => t.TaskId == created.TaskId);
        }
    }

    [Fact]
    public async Task AdvanceTaskStepAsync_UpdatesStepStatusAndCompletesTaskWhenAllStepsDone()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine) = CreateHarness();
        await using (os)
        {
            var req = new CreateTaskRequest(
                "Two Step Task",
                "Sequential run",
                "bob",
                [
                    new TaskStepDefinition("Step 1", "First", "cap1"),
                    new TaskStepDefinition("Step 2", "Second", "cap2")
                ]);

            var task = await engine.CreateTaskAsync(req, ct);

            var running = await engine.AdvanceTaskStepAsync(task.TaskId, 0, TaskStepStatus.Running, ct: ct);
            Assert.Equal(TaskStatus.Running, running.Status);
            Assert.Equal(TaskStepStatus.Running, running.Steps[0].Status);

            var step1Done = await engine.AdvanceTaskStepAsync(task.TaskId, 0, TaskStepStatus.Completed, "Artifact 1 created", ct);
            Assert.Equal(TaskStepStatus.Completed, step1Done.Steps[0].Status);
            Assert.Equal("Artifact 1 created", step1Done.Steps[0].Output);
            Assert.Equal(TaskStatus.Running, step1Done.Status);

            var step2Running = await engine.AdvanceTaskStepAsync(task.TaskId, 1, TaskStepStatus.Running, ct: ct);
            Assert.Equal(TaskStepStatus.Running, step2Running.Steps[1].Status);

            var completed = await engine.AdvanceTaskStepAsync(task.TaskId, 1, TaskStepStatus.Completed, "Artifact 2 created", ct);
            Assert.Equal(TaskStepStatus.Completed, completed.Steps[1].Status);
            Assert.Equal(TaskStatus.Completed, completed.Status);
            Assert.NotNull(completed.CompletedAt);
        }
    }

    [Fact]
    public async Task AdvanceTaskStepAsync_MarksTaskFailedWhenStepFails()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine) = CreateHarness();
        await using (os)
        {
            var req = new CreateTaskRequest(
                "Failing Task",
                "Will error",
                "carol",
                [
                    new TaskStepDefinition("Failing Step", "Fails", "cap.fault")
                ]);

            var task = await engine.CreateTaskAsync(req, ct);

            var failed = await engine.AdvanceTaskStepAsync(task.TaskId, 0, TaskStepStatus.Failed, "Connection refused", ct);
            Assert.Equal(TaskStatus.Failed, failed.Status);
            Assert.Equal(TaskStepStatus.Failed, failed.Steps[0].Status);
            Assert.Equal("Connection refused", failed.Steps[0].Output);
            Assert.NotNull(failed.CompletedAt);
        }
    }

    [Fact]
    public async Task VerifyTaskStepAsync_EnforcesPriorStepCompletionBarrier()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine) = CreateHarness();
        await using (os)
        {
            var req = new CreateTaskRequest(
                "Gated Task",
                "Barriered steps",
                "dave",
                [
                    new TaskStepDefinition("Step 0", "Must finish first", "cap.first"),
                    new TaskStepDefinition("Step 1", "Depends on 0", "cap.second")
                ]);

            var task = await engine.CreateTaskAsync(req, ct);

            // Step 1 verification should fail because Step 0 is not yet completed
            var preCheck = await engine.VerifyTaskStepAsync(task.TaskId, 1, ct);
            Assert.False(preCheck.Passed);
            Assert.NotNull(preCheck.Violations);
            Assert.True(preCheck.Violations.Length > 0);

            // Complete step 0
            await engine.AdvanceTaskStepAsync(task.TaskId, 0, TaskStepStatus.Completed, "Step 0 OK", ct);

            // Step 1 verification should now pass
            var postCheck = await engine.VerifyTaskStepAsync(task.TaskId, 1, ct);
            Assert.True(postCheck.Passed);
            Assert.NotNull(postCheck.Violations);
            Assert.Empty(postCheck.Violations);
        }
    }

    [Fact]
    public async Task RollbackTaskAsync_RollsBackStepsAndSetsStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine) = CreateHarness();
        await using (os)
        {
            var req = new CreateTaskRequest(
                "Rollback Task",
                "Task with rollback",
                "eve",
                [
                    new TaskStepDefinition("Step 0", "Init", "init"),
                    new TaskStepDefinition("Step 1", "Compute", "calc"),
                    new TaskStepDefinition("Step 2", "Publish", "pub")
                ]);

            var task = await engine.CreateTaskAsync(req, ct);
            await engine.AdvanceTaskStepAsync(task.TaskId, 0, TaskStepStatus.Completed, "Init done", ct);
            await engine.AdvanceTaskStepAsync(task.TaskId, 1, TaskStepStatus.Completed, "Compute done", ct);
            await engine.AdvanceTaskStepAsync(task.TaskId, 2, TaskStepStatus.Running, ct: ct);

            var rolledBack = await engine.RollbackTaskAsync(task.TaskId, 1, "Corrupted compute payload", ct);
            Assert.Equal(TaskStatus.RolledBack, rolledBack.Status);
            Assert.Equal("Corrupted compute payload", rolledBack.RollbackReason);
            Assert.Equal(TaskStepStatus.Completed, rolledBack.Steps[0].Status);
            Assert.Equal(TaskStepStatus.RolledBack, rolledBack.Steps[1].Status);
            Assert.Equal(TaskStepStatus.RolledBack, rolledBack.Steps[2].Status);
        }
    }

    [Fact]
    public async Task CancelTaskAsync_SetsCancelledStatusAndReason()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine) = CreateHarness();
        await using (os)
        {
            var req = new CreateTaskRequest(
                "Cancellable Task",
                "Abort mid flight",
                "frank",
                [
                    new TaskStepDefinition("Step 0", "Wait", "wait")
                ]);

            var task = await engine.CreateTaskAsync(req, ct);
            var cancelled = await engine.CancelTaskAsync(task.TaskId, "User aborted operation", ct);

            Assert.Equal(TaskStatus.Cancelled, cancelled.Status);
            Assert.Equal("User aborted operation", cancelled.RollbackReason);
            Assert.NotNull(cancelled.CompletedAt);
        }
    }

    [Fact]
    public void ResolveBrainId_RespectsStampedCallerAndExecutionContext()
    {
        var services = new ServiceCollection()
            .AddSingleton<IExecutionContextProvider, ExecutionContextProvider>()
            .BuildServiceProvider();
        var engine = new TaskEngine(services);

        Assert.Equal(new BrainId("local"), engine.ResolveBrainId());

        using (CallerContextStamper.StampScope(CapabilityCallers.Of("usr-1", "acc-1", "tenant-alpha")))
        {
            Assert.Equal(new BrainId("tenant-alpha"), engine.ResolveBrainId());
        }

        Assert.Equal(new BrainId("local"), engine.ResolveBrainId());
    }
}
