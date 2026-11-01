using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DigitalBrain;
using DigitalBrain.Testing;
using IntoChat.OS.Modules.AI.Tasks;
using IntoChat.OS.Modules.AI.Tasks.Signals;
using Xunit;
using TaskStatus = IntoChat.OS.Modules.AI.Tasks.TaskStatus;

namespace IntoChat.OS.Modules.AI.Tests.Unit.Tasks;

public sealed class TaskEngineStressChallengeFacts
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
    public async Task StateTransitions_FullLifecycle_TransitionsAndTimestampIntegrity()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine) = CreateHarness();
        await using (os)
        {
            var req = new CreateTaskRequest(
                "Lifecycle Task",
                "Full transition verification",
                "auditor",
                [
                    new TaskStepDefinition("Step 0", "First step", "cap.first"),
                    new TaskStepDefinition("Step 1", "Second step", "cap.second"),
                    new TaskStepDefinition("Step 2", "Third step", "cap.third")
                ]);

            var task = await engine.CreateTaskAsync(req, ct);
            Assert.Equal(TaskStatus.Pending, task.Status);
            Assert.Null(task.CompletedAt);

            // 1. Advance Step 0 to Running -> Task transitions to Running
            var t1 = await engine.AdvanceTaskStepAsync(task.TaskId, 0, TaskStepStatus.Running, ct: ct);
            Assert.Equal(TaskStatus.Running, t1.Status);
            Assert.Equal(TaskStepStatus.Running, t1.Steps[0].Status);
            Assert.NotNull(t1.Steps[0].StartedAt);
            Assert.Null(t1.Steps[0].CompletedAt);

            // 2. Complete Step 0
            var t2 = await engine.AdvanceTaskStepAsync(task.TaskId, 0, TaskStepStatus.Completed, "Artifact 0", ct);
            Assert.Equal(TaskStatus.Running, t2.Status);
            Assert.Equal(TaskStepStatus.Completed, t2.Steps[0].Status);
            Assert.NotNull(t2.Steps[0].CompletedAt);
            Assert.Equal("Artifact 0", t2.Steps[0].Output);

            // 3. Complete Step 1
            var t3 = await engine.AdvanceTaskStepAsync(task.TaskId, 1, TaskStepStatus.Completed, "Skipped/Bypassed", ct);
            Assert.Equal(TaskStatus.Running, t3.Status);
            Assert.Equal(TaskStepStatus.Completed, t3.Steps[1].Status);

            // 4. Complete Step 2 -> All steps completed -> Task transitions to Completed
            var t4 = await engine.AdvanceTaskStepAsync(task.TaskId, 2, TaskStepStatus.Completed, "Artifact 2", ct);
            Assert.Equal(TaskStatus.Completed, t4.Status);
            Assert.NotNull(t4.CompletedAt);
        }
    }

    [Fact]
    public async Task VerificationBarrier_EnforcesStrictSequentialDependencies()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine) = CreateHarness();
        await using (os)
        {
            var req = new CreateTaskRequest(
                "Barrier Stress",
                "Enforces verification barriers",
                "auditor",
                [
                    new TaskStepDefinition("Step 0", "S0", "cap.s0"),
                    new TaskStepDefinition("Step 1", "S1", "cap.s1"),
                    new TaskStepDefinition("Step 2", "S2", "cap.s2")
                ]);

            var task = await engine.CreateTaskAsync(req, ct);

            // Step 0 verification should pass (no prior steps)
            var v0 = await engine.VerifyTaskStepAsync(task.TaskId, 0, ct);
            Assert.True(v0.Passed);
            Assert.Empty(v0.Violations ?? []);

            // Step 1 and Step 2 verification must fail because prior steps are Pending
            var v1 = await engine.VerifyTaskStepAsync(task.TaskId, 1, ct);
            Assert.False(v1.Passed);
            Assert.Contains("Prior step 0 is not completed", v1.Summary);

            var v2 = await engine.VerifyTaskStepAsync(task.TaskId, 2, ct);
            Assert.False(v2.Passed);
            Assert.Contains("Prior step 0 is not completed", v2.Summary);

            // Complete Step 0, but fail Step 1
            await engine.AdvanceTaskStepAsync(task.TaskId, 0, TaskStepStatus.Completed, "S0 done", ct);
            await engine.AdvanceTaskStepAsync(task.TaskId, 1, TaskStepStatus.Failed, "Compilation failed with CS0103", ct);

            // Step 1 self-verification must fail because its own status is Failed
            var v1Self = await engine.VerifyTaskStepAsync(task.TaskId, 1, ct);
            Assert.False(v1Self.Passed);
            Assert.Contains(v1Self.Violations ?? [], msg => msg.Contains("Compilation failed with CS0103"));

            // Step 2 verification must fail because Step 1 is Failed
            var v2AfterFail = await engine.VerifyTaskStepAsync(task.TaskId, 2, ct);
            Assert.False(v2AfterFail.Passed);
            Assert.Contains("Prior step 1 is not completed (status: Failed)", v2AfterFail.Summary);

            // Invalid step indices must return clean failure, never crash
            var vNegative = await engine.VerifyTaskStepAsync(task.TaskId, -1, ct);
            Assert.False(vNegative.Passed);
            Assert.Contains("invalid", vNegative.Summary, StringComparison.OrdinalIgnoreCase);

            var vOverflow = await engine.VerifyTaskStepAsync(task.TaskId, 999, ct);
            Assert.False(vOverflow.Passed);
            Assert.Contains("invalid", vOverflow.Summary, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task RollbackTaskAsync_FromRunningStep_RollsBackStepsSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine) = CreateHarness();
        await using (os)
        {
            var req = new CreateTaskRequest(
                "Self-Repair Pipeline",
                "Task supporting repair loop",
                "bot",
                [
                    new TaskStepDefinition("Plan", "Formulate plan", "planner"),
                    new TaskStepDefinition("Code", "Generate code", "coder", RollbackCapability: "revert_git"),
                    new TaskStepDefinition("Test", "Run tests", "tester"),
                    new TaskStepDefinition("Deploy", "Release to staging", "deployer")
                ]);

            var task = await engine.CreateTaskAsync(req, ct);

            // Advance Plan and Code to Completed, Test to Running
            await engine.AdvanceTaskStepAsync(task.TaskId, 0, TaskStepStatus.Completed, "Plan V1", ct);
            await engine.AdvanceTaskStepAsync(task.TaskId, 1, TaskStepStatus.Completed, "Code V1 written", ct);
            await engine.AdvanceTaskStepAsync(task.TaskId, 2, TaskStepStatus.Running, ct: ct);

            // Rollback to Step 1 (Code) from Running state
            var rolledBack = await engine.RollbackTaskAsync(task.TaskId, 1, "Self-repair: test failure indicates syntax bug", ct);

            // Verify status and step states
            Assert.Equal(TaskStatus.RolledBack, rolledBack.Status);
            Assert.Equal("Self-repair: test failure indicates syntax bug", rolledBack.RollbackReason);

            // Step 0 (Plan) remains Completed
            Assert.Equal(TaskStepStatus.Completed, rolledBack.Steps[0].Status);
            Assert.Equal("Plan V1", rolledBack.Steps[0].Output);

            // Steps 1 and 2 are RolledBack
            Assert.Equal(TaskStepStatus.RolledBack, rolledBack.Steps[1].Status);
            Assert.Equal(TaskStepStatus.RolledBack, rolledBack.Steps[2].Status);
            Assert.Equal(TaskStepStatus.Pending, rolledBack.Steps[3].Status);

            // Self-repair re-execution:
            // 1. Re-advance Step 1 (Code) to Running then Completed with fixed code
            await engine.AdvanceTaskStepAsync(task.TaskId, 1, TaskStepStatus.Running, ct: ct);
            await engine.AdvanceTaskStepAsync(task.TaskId, 1, TaskStepStatus.Completed, "Code V2 (fixed)", ct);

            // 2. Re-advance Step 2 (Test) to Running then Completed
            await engine.AdvanceTaskStepAsync(task.TaskId, 2, TaskStepStatus.Running, ct: ct);
            await engine.AdvanceTaskStepAsync(task.TaskId, 2, TaskStepStatus.Completed, "All tests passed (100%)", ct);

            // 3. Advance Step 3 (Deploy) to Completed
            var recoveredTask = await engine.AdvanceTaskStepAsync(task.TaskId, 3, TaskStepStatus.Completed, "Deployed to staging", ct);

            // The task transitions to Completed when all steps reach Completed
            Assert.Equal(TaskStatus.Completed, recoveredTask.Status);
            Assert.NotNull(recoveredTask.CompletedAt);
        }
    }

    [Fact]
    public async Task RollbackTaskAsync_FromFailedStep_RollsBackStepCleanly()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine) = CreateHarness();
        await using (os)
        {
            var req = new CreateTaskRequest(
                "Failing Task with Rollback",
                "Demonstrates rollback behavior on failed step",
                "tester",
                [
                    new TaskStepDefinition("Step 0", "Init", "init"),
                    new TaskStepDefinition("Step 1", "Compute", "calc"),
                    new TaskStepDefinition("Step 2", "Validate", "validator")
                ]);

            var task = await engine.CreateTaskAsync(req, ct);
            await engine.AdvanceTaskStepAsync(task.TaskId, 0, TaskStepStatus.Completed, "Init OK", ct);
            await engine.AdvanceTaskStepAsync(task.TaskId, 1, TaskStepStatus.Completed, "Compute OK", ct);

            // Step 2 Fails
            await engine.AdvanceTaskStepAsync(task.TaskId, 2, TaskStepStatus.Failed, "Validation assertion failed: threshold < 0.9", ct);

            // Rollback to Step 1
            var rolledBack = await engine.RollbackTaskAsync(task.TaskId, 1, "Rollback to recompute", ct);

            // Rollback to Step 1 transitions Step 1 and Failed Step 2 to RolledBack
            Assert.Equal(TaskStatus.RolledBack, rolledBack.Status);
            Assert.Equal(TaskStepStatus.RolledBack, rolledBack.Steps[1].Status);
            Assert.Equal(TaskStepStatus.RolledBack, rolledBack.Steps[2].Status);

            // Re-executing from RolledBack transitions task status to Running
            var runningTask = await engine.AdvanceTaskStepAsync(task.TaskId, 1, TaskStepStatus.Running, ct: ct);
            Assert.Equal(TaskStatus.Running, runningTask.Status);

            await engine.AdvanceTaskStepAsync(task.TaskId, 1, TaskStepStatus.Completed, "Compute OK (recomputed)", ct);
            await engine.AdvanceTaskStepAsync(task.TaskId, 2, TaskStepStatus.Running, ct: ct);
            var completedTask = await engine.AdvanceTaskStepAsync(task.TaskId, 2, TaskStepStatus.Completed, "Validation passed", ct);

            Assert.Equal(TaskStatus.Completed, completedTask.Status);
            Assert.NotNull(completedTask.CompletedAt);

            var verify = await engine.VerifyTaskStepAsync(task.TaskId, 2, ct);
            Assert.True(verify.Passed);
        }
    }

    [Fact]
    public void EphemeralSignalAttribute_EnforcesHotPathSeparationOnTaskSignals()
    {
        // 1. TaskStepAdvanced MUST carry [EphemeralSignal] to bypass disk journaling
        var stepAdvancedAttr = typeof(TaskStepAdvanced).GetCustomAttribute<EphemeralSignalAttribute>();
        Assert.NotNull(stepAdvancedAttr);

        var stepAdvancedSignal = typeof(TaskStepAdvanced).GetCustomAttribute<SignalAttribute>();
        Assert.NotNull(stepAdvancedSignal);
        Assert.Equal("tasks.step-advanced", stepAdvancedSignal.Alias);

        // 2. Cold-path signals MUST NOT carry [EphemeralSignal] (they require durable persistence)
        Assert.Null(typeof(TaskCreated).GetCustomAttribute<EphemeralSignalAttribute>());
        Assert.NotNull(typeof(TaskCreated).GetCustomAttribute<SignalAttribute>());

        Assert.Null(typeof(TaskCompleted).GetCustomAttribute<EphemeralSignalAttribute>());
        Assert.NotNull(typeof(TaskCompleted).GetCustomAttribute<SignalAttribute>());

        Assert.Null(typeof(TaskRolledBack).GetCustomAttribute<EphemeralSignalAttribute>());
        Assert.NotNull(typeof(TaskRolledBack).GetCustomAttribute<SignalAttribute>());

        Assert.Null(typeof(TaskDirectoryChanged).GetCustomAttribute<EphemeralSignalAttribute>());
        Assert.NotNull(typeof(TaskDirectoryChanged).GetCustomAttribute<SignalAttribute>());
    }

    [Fact]
    public async Task ConcurrencyStress_MultipleStepUpdatesDoNotCorruptState()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine) = CreateHarness();
        await using (os)
        {
            const int stepCount = 10;
            var stepDefs = Enumerable.Range(0, stepCount)
                .Select(i => new TaskStepDefinition($"Step {i}", $"Concurrent step {i}", $"cap.{i}"))
                .ToArray();

            var req = new CreateTaskRequest("Concurrent Task", "Hammering step progression", "stress_tester", stepDefs);
            var task = await engine.CreateTaskAsync(req, ct);

            // Concurrently advance all steps from 10 parallel tasks
            var tasks = Enumerable.Range(0, stepCount).Select(async i =>
            {
                await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Running, ct: ct);
                await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Completed, $"Output from step {i}", ct);
            });

            await Task.WhenAll(tasks);

            // Fetch final task state and verify every step is intact
            var finalTask = await engine.GetTaskAsync(task.TaskId, ct);
            Assert.NotNull(finalTask);
            Assert.Equal(stepCount, finalTask.Steps.Length);
            Assert.Equal(TaskStatus.Completed, finalTask.Status);

            for (var i = 0; i < stepCount; i++)
            {
                Assert.Equal(TaskStepStatus.Completed, finalTask.Steps[i].Status);
                Assert.Equal($"Output from step {i}", finalTask.Steps[i].Output);
                Assert.NotNull(finalTask.Steps[i].StartedAt);
                Assert.NotNull(finalTask.Steps[i].CompletedAt);
            }
        }
    }

    [Fact]
    public async Task TaskDirectory_QueryFilteringByOwnerAndStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine) = CreateHarness();
        await using (os)
        {
            // Create tasks across 3 users
            var alice1 = await engine.CreateTaskAsync(new CreateTaskRequest("A1", "Alice Task 1", "alice", [new("S0", "D0", "C0")]), ct);
            var alice2 = await engine.CreateTaskAsync(new CreateTaskRequest("A2", "Alice Task 2", "alice", [new("S0", "D0", "C0")]), ct);
            var bob1 = await engine.CreateTaskAsync(new CreateTaskRequest("B1", "Bob Task 1", "bob", [new("S0", "D0", "C0")]), ct);

            // Complete alice1
            await engine.AdvanceTaskStepAsync(alice1.TaskId, 0, TaskStepStatus.Completed, ct: ct);

            // Query by owner alice
            var aliceTasks = await engine.ListTasksAsync("alice", ct: ct);
            Assert.Equal(2, aliceTasks.Count);
            Assert.All(aliceTasks, t => Assert.Equal("alice", t.Owner));

            // Query by owner bob
            var bobTasks = await engine.ListTasksAsync("bob", ct: ct);
            Assert.Single(bobTasks);
            Assert.Equal("bob", bobTasks[0].Owner);

            // Query by status Completed
            var completedTasks = await engine.ListTasksAsync(status: TaskStatus.Completed, ct: ct);
            Assert.Single(completedTasks);
            Assert.Equal(alice1.TaskId, completedTasks[0].TaskId);

            // Query with non-existent owner
            var unknownTasks = await engine.ListTasksAsync("unknown_user", ct: ct);
            Assert.Empty(unknownTasks);
        }
    }

    [Fact]
    public async Task BoundaryConditions_InvalidIndicesAndEmptyTask()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine) = CreateHarness();
        await using (os)
        {
            var req = new CreateTaskRequest("Boundary Task", "Bounds check", "qa", [new("S0", "D0", "C0")]);
            var task = await engine.CreateTaskAsync(req, ct);

            // Out-of-bounds advance
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => engine.AdvanceTaskStepAsync(task.TaskId, -1, TaskStepStatus.Running, ct: ct));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => engine.AdvanceTaskStepAsync(task.TaskId, 5, TaskStepStatus.Running, ct: ct));

            // Out-of-bounds rollback
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => engine.RollbackTaskAsync(task.TaskId, -1, "reason", ct));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => engine.RollbackTaskAsync(task.TaskId, 10, "reason", ct));

            // Null or empty taskId
            await Assert.ThrowsAsync<ArgumentException>(() => engine.AdvanceTaskStepAsync("", 0, TaskStepStatus.Running, ct: ct));
            await Assert.ThrowsAsync<ArgumentException>(() => engine.GetTaskAsync("", ct));
            await Assert.ThrowsAsync<ArgumentException>(() => engine.CancelTaskAsync("", "reason", ct));
        }
    }
}
