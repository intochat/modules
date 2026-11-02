using System;
using System.Linq;
using System.Threading.Tasks;
using DigitalBrain;
using DigitalBrain.Testing;
using IntoChat.OS.Modules.AI.Tasks;
using IntoChat.OS.Modules.Microsoft.GitHub.Signals;
using Xunit;
using TaskStatus = IntoChat.OS.Modules.AI.Tasks.TaskStatus;

namespace IntoChat.OS.Modules.AI.Tests.Unit.Tasks;

public sealed class TaskDevLoopFacts
{
    private static (TestOS os, TaskEngine engine, Func<string, TaskNeuron> getNeuron) CreateHarness()
    {
        var os = TestOS.Local();
        os.Register<TaskNeuron>();
        os.Register<TaskDirectoryNeuron>();

        var engine = new TaskEngine(
            taskId => os.Get<ITaskNeuron>(new NeuronId(new BrainId("local"), "tasks.task", taskId)),
            () => os.Get<ITaskDirectoryNeuron>(new NeuronId(new BrainId("local"), "tasks.directory", "default")));

        TaskNeuron GetNeuron(string taskId)
        {
            var host = os.GetType().GetMethod("Host", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(os, [new NeuronId(new BrainId("local"), "tasks.task", taskId)])!;
            return (TaskNeuron)host.GetType().GetProperty("CurrentNeuron", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(host)!;
        }

        return (os, engine, GetNeuron);
    }

    [Fact]
    public void BotIdentity_RecognizesBotsAndRejectsHumans()
    {
        Assert.False(BotIdentity.IsBot(null));
        Assert.False(BotIdentity.IsBot(""));
        Assert.False(BotIdentity.IsBot("   "));
        Assert.False(BotIdentity.IsBot("alice"));
        Assert.False(BotIdentity.IsBot("bob-contributor"));

        Assert.True(BotIdentity.IsBot("intochat-bot[bot]"));
        Assert.True(BotIdentity.IsBot("INTOCHAT-BOT[BOT]"));
        Assert.True(BotIdentity.IsBot("github-actions[bot]"));
        Assert.True(BotIdentity.IsBot("dependabot[bot]"));
        Assert.True(BotIdentity.IsBot("custom-ci[bot]"));
    }

    [Fact]
    public void CreateDevPipelineRequest_CreatesCanonical8Stages()
    {
        var request = TaskEngine.CreateDevPipelineRequest("Feature Login", "Implement OAuth login flow", "alice", "feature/login");

        Assert.Equal("Feature Login", request.Title);
        Assert.Equal("alice", request.Owner);
        Assert.Equal(8, request.Steps.Length);

        Assert.Equal("Plan", request.Steps[0].Title);
        Assert.Equal("ai.plan", request.Steps[0].Capability);
        Assert.NotNull(request.Steps[0].IdempotencyKey);

        Assert.Equal("Edit", request.Steps[1].Title);
        Assert.Equal("microsoft.roslyn.edit", request.Steps[1].Capability);
        Assert.NotNull(request.Steps[1].IdempotencyKey);

        Assert.Equal("Test", request.Steps[2].Title);
        Assert.Equal("test.verification", request.Steps[2].Capability);
        Assert.NotNull(request.Steps[2].IdempotencyKey);

        Assert.Equal("Candidate", request.Steps[3].Title);
        Assert.Equal("aspire.candidates", request.Steps[3].Capability);
        Assert.NotNull(request.Steps[3].IdempotencyKey);

        Assert.Equal("Open PR", request.Steps[4].Title);
        Assert.Equal("github.open-pr", request.Steps[4].Capability);
        Assert.NotNull(request.Steps[4].IdempotencyKey);

        Assert.Equal("Await Review", request.Steps[5].Title);
        Assert.Equal("github.await-review", request.Steps[5].Capability);
        Assert.NotNull(request.Steps[5].IdempotencyKey);

        Assert.Equal("Merge [Confirm]", request.Steps[6].Title);
        Assert.Equal("github.merge", request.Steps[6].Capability);
        Assert.NotNull(request.Steps[6].IdempotencyKey);

        Assert.Equal("Roll Out [Confirm]", request.Steps[7].Title);
        Assert.Equal("cluster.rollout", request.Steps[7].Capability);
        Assert.NotNull(request.Steps[7].IdempotencyKey);
    }

    [Fact]
    public async Task EightStageDevLoop_PausesAtConfirmationGates_AndResumesViaConfirmStep()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine, _) = CreateHarness();
        await using (os)
        {
            var req = TaskEngine.CreateDevPipelineRequest("Feature Auth", "Add auth", "alice", "feature/auth");
            var task = await engine.CreateTaskAsync(req, ct);

            // Advance stages 0 to 5 to Completed
            for (var i = 0; i <= 5; i++)
            {
                task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Running, ct: ct);
                task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Completed, $"Output {i}", ct);
            }

            Assert.Equal(TaskStatus.Running, task.Status);

            // Advance stage 6 (Merge [Confirm]) to Running -> Should enter AwaitingConfirmation
            task = await engine.AdvanceTaskStepAsync(task.TaskId, 6, TaskStepStatus.Running, ct: ct);
            Assert.Equal(TaskStepStatus.AwaitingConfirmation, task.Steps[6].Status);
            Assert.Equal(TaskStatus.AwaitingConfirmation, task.Status);

            // Human Confirmation resumes execution
            task = await engine.ConfirmTaskStepAsync(task.TaskId, 6, ct: ct);
            Assert.Equal(TaskStepStatus.Running, task.Steps[6].Status);
            Assert.Equal(TaskStatus.Running, task.Status);

            // Complete Merge step
            task = await engine.AdvanceTaskStepAsync(task.TaskId, 6, TaskStepStatus.Completed, "Merged PR #42", ct);
            Assert.Equal(TaskStepStatus.Completed, task.Steps[6].Status);

            // Advance stage 7 (Roll Out [Confirm]) to Running -> Should enter AwaitingConfirmation
            task = await engine.AdvanceTaskStepAsync(task.TaskId, 7, TaskStepStatus.Running, ct: ct);
            Assert.Equal(TaskStepStatus.AwaitingConfirmation, task.Steps[7].Status);
            Assert.Equal(TaskStatus.AwaitingConfirmation, task.Status);

            // Human Confirmation for Roll Out
            task = await engine.ConfirmTaskStepAsync(task.TaskId, 7, ct: ct);
            Assert.Equal(TaskStepStatus.Running, task.Steps[7].Status);

            // Complete Roll Out -> Task completes
            task = await engine.AdvanceTaskStepAsync(task.TaskId, 7, TaskStepStatus.Completed, "Deployed to cluster", ct);
            Assert.Equal(TaskStepStatus.Completed, task.Steps[7].Status);
            Assert.Equal(TaskStatus.Completed, task.Status);
            Assert.NotNull(task.CompletedAt);
        }
    }

    [Fact]
    public async Task ConfirmTaskStepAsync_Throws_WhenStepNotAwaitingConfirmation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine, _) = CreateHarness();
        await using (os)
        {
            var req = TaskEngine.CreateDevPipelineRequest("Task 1", "Desc", "alice", "main");
            var task = await engine.CreateTaskAsync(req, ct);

            // Step 0 is Pending, not AwaitingConfirmation
            await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ConfirmTaskStepAsync(task.TaskId, 0, ct: ct));
        }
    }

    [Fact]
    public async Task BotComment_IsIgnored_AndDoesNotAppendRepairStep()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine, _) = CreateHarness();
        await using (os)
        {
            var req = TaskEngine.CreateDevPipelineRequest("Task 2", "Desc", "alice", "main");
            var task = await engine.CreateTaskAsync(req, ct);

            // Advance to Await Review
            for (var i = 0; i <= 5; i++)
            {
                task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Running, ct: ct);
                task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Completed, ct: ct);
            }

            var initialStepCount = task.Steps.Length;

            // Direct call to TaskNeuron with Bot Comment
            var neuron = os.Get<ITaskNeuron>(new NeuronId(new BrainId("local"), "tasks.task", task.TaskId));
            var botSignal = new PullRequestChanged(10, "rev1", "key", null, "intochat-bot[bot]", "Bot automated message");

            // Invoke OnPullRequestChanged via signal delivery or neuron
            var hostMethod = neuron.GetType().GetMethod("OnPullRequestChanged");
            if (hostMethod is not null)
            {
                await (Task)hostMethod.Invoke(neuron, [botSignal])!;
            }

            var current = await engine.GetTaskAsync(task.TaskId, ct);
            Assert.NotNull(current);
            Assert.Equal(initialStepCount, current.Steps.Length);
        }
    }

    [Fact]
    public async Task ExternalReviewerComment_AppendsRepairStep_AndTransitionsBackToEdit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine, _) = CreateHarness();
        await using (os)
        {
            var req = TaskEngine.CreateDevPipelineRequest("Task 3", "Desc", "alice", "main");
            var task = await engine.CreateTaskAsync(req, ct);

            // Advance to Await Review
            for (var i = 0; i <= 5; i++)
            {
                task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Running, ct: ct);
                task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Completed, ct: ct);
            }

            var initialStepCount = task.Steps.Length;

            // Deliver external reviewer comment
            var neuronProxy = os.Get<ITaskNeuron>(new NeuronId(new BrainId("local"), "tasks.task", task.TaskId));
            var reviewerSignal = new PullRequestChanged(10, "rev1", "key", null, "octocat", "Fix the null check on line 15");

            // Call OnPullRequestChanged through neuron turn
            var host = os.GetType().GetMethod("Host", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(os, [new NeuronId(new BrainId("local"), "tasks.task", task.TaskId)])!;
            var neuron = (TaskNeuron)host.GetType().GetProperty("CurrentNeuron", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(host)!;

            await neuron.OnPullRequestChanged(reviewerSignal);

            var current = await engine.GetTaskAsync(task.TaskId, ct);
            Assert.NotNull(current);
            Assert.Equal(initialStepCount + 1, current.Steps.Length);

            var repairStep = current.Steps.Last();
            Assert.Equal("Repair", repairStep.Title);
            Assert.Contains("octocat", repairStep.Description);
            Assert.Contains("line 15", repairStep.Description);
            Assert.Equal("microsoft.roslyn.edit", repairStep.Capability);

            // Edit step (index 1) is now Running
            Assert.Equal(TaskStepStatus.Running, current.Steps[1].Status);
            Assert.Equal(TaskStatus.Running, current.Status);
        }
    }

    [Fact]
    public async Task CiFailure_AppendsRepairStep_AndTransitionsBackToEdit()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine, _) = CreateHarness();
        await using (os)
        {
            var req = TaskEngine.CreateDevPipelineRequest("Task 4", "Desc", "alice", "main");
            var task = await engine.CreateTaskAsync(req, ct);

            // Advance to Await Review
            for (var i = 0; i <= 5; i++)
            {
                task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Running, ct: ct);
                task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Completed, ct: ct);
            }

            var initialStepCount = task.Steps.Length;

            var ciFailureSignal = new PullRequestChanged(10, "rev1", "key", "failure", null, null);

            var host = os.GetType().GetMethod("Host", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(os, [new NeuronId(new BrainId("local"), "tasks.task", task.TaskId)])!;
            var neuron = (TaskNeuron)host.GetType().GetProperty("CurrentNeuron", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(host)!;

            await neuron.OnPullRequestChanged(ciFailureSignal);

            var current = await engine.GetTaskAsync(task.TaskId, ct);
            Assert.NotNull(current);
            Assert.Equal(initialStepCount + 1, current.Steps.Length);

            var repairStep = current.Steps.Last();
            Assert.Equal("Repair", repairStep.Title);
            Assert.Contains("CI failure", repairStep.Description);
            Assert.Equal("microsoft.roslyn.edit", repairStep.Capability);

            // Edit step is Running
            Assert.Equal(TaskStepStatus.Running, current.Steps[1].Status);
            Assert.Equal(TaskStatus.Running, current.Status);
        }
    }

    [Fact]
    public async Task ReDriveAsync_ReDrivesStalledRunningStep_WithIdempotencyKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine, _) = CreateHarness();
        await using (os)
        {
            var req = TaskEngine.CreateDevPipelineRequest("Task 5", "Desc", "alice", "main");
            var task = await engine.CreateTaskAsync(req, ct);

            // Start step 0
            task = await engine.AdvanceTaskStepAsync(task.TaskId, 0, TaskStepStatus.Running, ct: ct);
            var originalIdempotencyKey = task.Steps[0].IdempotencyKey;
            Assert.NotNull(originalIdempotencyKey);

            var host = os.GetType().GetMethod("Host", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(os, [new NeuronId(new BrainId("local"), "tasks.task", task.TaskId)])!;
            var neuron = (TaskNeuron)host.GetType().GetProperty("CurrentNeuron", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(host)!;

            // Trigger re-drive
            await neuron.ReDriveAsync();

            var current = await engine.GetTaskAsync(task.TaskId, ct);
            Assert.NotNull(current);
            Assert.Equal(TaskStepStatus.Running, current.Steps[0].Status);
            Assert.Equal(originalIdempotencyKey, current.Steps[0].IdempotencyKey);
        }
    }

    [Fact]
    public void TaskNeuron_Implements_IRemindable()
    {
        Assert.True(typeof(Orleans.IRemindable).IsAssignableFrom(typeof(TaskNeuron)));
    }

    [Fact]
    public async Task ConfirmTaskStepAsync_Throws_WhenTaskIsAwaitingConfirmation_ButStepIsNotAwaitingConfirmation()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine, _) = CreateHarness();
        await using (os)
        {
            var req = TaskEngine.CreateDevPipelineRequest("Task Invariant 1", "Desc", "alice", "main");
            var task = await engine.CreateTaskAsync(req, ct);

            // Advance steps 0 to 5 to Completed
            for (var i = 0; i <= 5; i++)
            {
                task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Running, ct: ct);
                task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Completed, ct: ct);
            }

            // Advance step 6 (Merge [Confirm]) to Running -> enters AwaitingConfirmation
            task = await engine.AdvanceTaskStepAsync(task.TaskId, 6, TaskStepStatus.Running, ct: ct);
            Assert.Equal(TaskStatus.AwaitingConfirmation, task.Status);
            Assert.Equal(TaskStepStatus.AwaitingConfirmation, task.Steps[6].Status);

            // Attempting to confirm step 0 (which is Completed) or step 7 (which is Pending) must throw InvalidOperationException
            await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ConfirmTaskStepAsync(task.TaskId, 0, ct: ct));
            await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ConfirmTaskStepAsync(task.TaskId, 7, ct: ct));

            // Verify task and steps remain in their expected states
            var current = await engine.GetTaskAsync(task.TaskId, ct);
            Assert.NotNull(current);
            Assert.Equal(TaskStatus.AwaitingConfirmation, current.Status);
            Assert.Equal(TaskStepStatus.Completed, current.Steps[0].Status);
            Assert.Equal(TaskStepStatus.AwaitingConfirmation, current.Steps[6].Status);
            Assert.Equal(TaskStepStatus.Pending, current.Steps[7].Status);
        }
    }

    [Fact]
    public async Task AdvanceTaskStepAsync_DirectlyToCompleted_OnConfirmationGate_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine, _) = CreateHarness();
        await using (os)
        {
            var req = TaskEngine.CreateDevPipelineRequest("Task Invariant 2", "Desc", "alice", "main");
            var task = await engine.CreateTaskAsync(req, ct);

            // 1. Advance steps 0 to 5 to Completed
            for (var i = 0; i <= 5; i++)
            {
                task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Running, ct: ct);
                task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Completed, ct: ct);
            }

            // Direct jump from Pending to Completed on gate step 6 must throw
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                engine.AdvanceTaskStepAsync(task.TaskId, 6, TaskStepStatus.Completed, ct: ct));

            // Advance step 6 to Running -> enters AwaitingConfirmation
            task = await engine.AdvanceTaskStepAsync(task.TaskId, 6, TaskStepStatus.Running, ct: ct);
            Assert.Equal(TaskStepStatus.AwaitingConfirmation, task.Steps[6].Status);

            // Direct jump from AwaitingConfirmation to Completed without calling ConfirmTaskStepAsync must throw
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                engine.AdvanceTaskStepAsync(task.TaskId, 6, TaskStepStatus.Completed, ct: ct));

            // Verify status is unchanged
            var current = await engine.GetTaskAsync(task.TaskId, ct);
            Assert.NotNull(current);
            Assert.Equal(TaskStepStatus.AwaitingConfirmation, current.Steps[6].Status);
            Assert.Equal(TaskStatus.AwaitingConfirmation, current.Status);
        }
    }

    [Theory]
    [InlineData(TaskStatus.Completed)]
    [InlineData(TaskStatus.Cancelled)]
    [InlineData(TaskStatus.Failed)]
    public async Task OnPullRequestChanged_TerminalTask_IgnoresLatePrEvents_AndDoesNotResurrect(TaskStatus terminalStatus)
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine, _) = CreateHarness();
        await using (os)
        {
            var req = TaskEngine.CreateDevPipelineRequest($"Task Invariant 3 {terminalStatus}", "Desc", "alice", "main");
            var task = await engine.CreateTaskAsync(req, ct);

            // Advance all steps or fail/cancel to reach terminal state
            if (terminalStatus == TaskStatus.Completed)
            {
                for (var i = 0; i <= 5; i++)
                {
                    task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Running, ct: ct);
                    task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Completed, ct: ct);
                }
                task = await engine.AdvanceTaskStepAsync(task.TaskId, 6, TaskStepStatus.Running, ct: ct);
                task = await engine.ConfirmTaskStepAsync(task.TaskId, 6, ct: ct);
                task = await engine.AdvanceTaskStepAsync(task.TaskId, 6, TaskStepStatus.Completed, ct: ct);

                task = await engine.AdvanceTaskStepAsync(task.TaskId, 7, TaskStepStatus.Running, ct: ct);
                task = await engine.ConfirmTaskStepAsync(task.TaskId, 7, ct: ct);
                task = await engine.AdvanceTaskStepAsync(task.TaskId, 7, TaskStepStatus.Completed, ct: ct);
            }
            else if (terminalStatus == TaskStatus.Failed)
            {
                task = await engine.AdvanceTaskStepAsync(task.TaskId, 0, TaskStepStatus.Failed, "Initial failure", ct);
            }
            else if (terminalStatus == TaskStatus.Cancelled)
            {
                task = await engine.CancelTaskAsync(task.TaskId, "Cancelled by user", ct);
            }

            Assert.Equal(terminalStatus, task.Status);
            var stepCountBefore = task.Steps.Length;

            // Obtain TaskNeuron host
            var host = os.GetType().GetMethod("Host", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(os, [new NeuronId(new BrainId("local"), "tasks.task", task.TaskId)])!;
            var neuron = (TaskNeuron)host.GetType().GetProperty("CurrentNeuron", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(host)!;

            // Late human reviewer comment
            await neuron.OnPullRequestChanged(new PullRequestChanged(42, "rev1", "key", null, "octocat", "Late review comment"));
            var stateAfterComment = await engine.GetTaskAsync(task.TaskId, ct);
            Assert.NotNull(stateAfterComment);
            Assert.Equal(terminalStatus, stateAfterComment.Status);
            Assert.Equal(stepCountBefore, stateAfterComment.Steps.Length);

            // Late CI failure event
            await neuron.OnPullRequestChanged(new PullRequestChanged(42, "rev2", "key", "failure", null, null));
            var stateAfterCi = await engine.GetTaskAsync(task.TaskId, ct);
            Assert.NotNull(stateAfterCi);
            Assert.Equal(terminalStatus, stateAfterCi.Status);
            Assert.Equal(stepCountBefore, stateAfterCi.Steps.Length);
        }
    }

    [Fact]
    public async Task OnPullRequestChanged_CiFailureWithBotAuthor_IsProcessedAndTriggersRepair()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine, _) = CreateHarness();
        await using (os)
        {
            var req = TaskEngine.CreateDevPipelineRequest("Task Invariant 4", "Desc", "alice", "main");
            var task = await engine.CreateTaskAsync(req, ct);

            // Advance to Await Review
            for (var i = 0; i <= 5; i++)
            {
                task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Running, ct: ct);
                task = await engine.AdvanceTaskStepAsync(task.TaskId, i, TaskStepStatus.Completed, ct: ct);
            }

            var initialStepCount = task.Steps.Length;

            // Signal has CiStatus == "failure" AND CommentAuthor is a bot (e.g. github-actions[bot])
            var botCiFailureSignal = new PullRequestChanged(10, "rev1", "key", "failure", "github-actions[bot]", "CI workflow failed on step 3");

            var host = os.GetType().GetMethod("Host", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(os, [new NeuronId(new BrainId("local"), "tasks.task", task.TaskId)])!;
            var neuron = (TaskNeuron)host.GetType().GetProperty("CurrentNeuron", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(host)!;

            await neuron.OnPullRequestChanged(botCiFailureSignal);

            var current = await engine.GetTaskAsync(task.TaskId, ct);
            Assert.NotNull(current);
            Assert.Equal(initialStepCount + 1, current.Steps.Length);

            var repairStep = current.Steps.Last();
            Assert.Equal("Repair", repairStep.Title);
            Assert.Equal("Repair CI failure", repairStep.Description);
            Assert.Equal("microsoft.roslyn.edit", repairStep.Capability);

            // Step 1 (Edit) transitioned back to Running
            Assert.Equal(TaskStepStatus.Running, current.Steps[1].Status);
            Assert.Equal(TaskStatus.Running, current.Status);
        }
    }

    [Fact]
    public async Task ReDriveAsync_WhenIdempotencyKeyIsNull_GeneratesAndPersistsIdempotencyKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var (os, engine, _) = CreateHarness();
        await using (os)
        {
            // Create a task request where step 0 has null IdempotencyKey
            var step0 = new TaskStepDefinition("Step 0", "Description", "some.capability", IdempotencyKey: null);
            var req = new CreateTaskRequest("Task Invariant 5", "Desc", "alice", [step0]);
            var task = await engine.CreateTaskAsync(req, ct);

            // Start step 0
            task = await engine.AdvanceTaskStepAsync(task.TaskId, 0, TaskStepStatus.Running, ct: ct);
            Assert.Null(task.Steps[0].IdempotencyKey);

            var host = os.GetType().GetMethod("Host", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(os, [new NeuronId(new BrainId("local"), "tasks.task", task.TaskId)])!;
            var neuron = (TaskNeuron)host.GetType().GetProperty("CurrentNeuron", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(host)!;

            // Trigger re-drive
            await neuron.ReDriveAsync();

            // Load fresh from engine / storage
            var current = await engine.GetTaskAsync(task.TaskId, ct);
            Assert.NotNull(current);
            Assert.Equal(TaskStepStatus.Running, current.Steps[0].Status);
            var generatedKey = current.Steps[0].IdempotencyKey;
            Assert.NotNull(generatedKey);
            Assert.StartsWith("step-0-", generatedKey, StringComparison.Ordinal);

            // Trigger re-drive second time — idempotency key must remain identical and not regenerate
            await neuron.ReDriveAsync();
            var secondLoad = await engine.GetTaskAsync(task.TaskId, ct);
            Assert.NotNull(secondLoad);
            Assert.Equal(generatedKey, secondLoad.Steps[0].IdempotencyKey);
        }
    }
}
