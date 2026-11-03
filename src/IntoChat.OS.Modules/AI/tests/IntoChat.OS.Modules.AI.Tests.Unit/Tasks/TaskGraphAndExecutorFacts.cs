using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DigitalBrain;
using DigitalBrain.Graph;
using DigitalBrain.Spi;
using DigitalBrain.Testing;
using IntoChat.OS.Modules.AI.Agents.Runs;
using IntoChat.OS.Modules.AI.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TaskStatus = IntoChat.OS.Modules.AI.Tasks.TaskStatus;

namespace IntoChat.OS.Modules.AI.Tests.Unit.Tasks;

public sealed class TaskGraphAndExecutorFacts
{
    private static bool HasEdge(GraphSnapshot graph, int from, int to, GraphEdgeKind kind)
    {
        for (var e = graph.RowStart[from]; e < graph.RowStart[from + 1]; e++)
        {
            if (graph.Col[e] == to && graph.EdgeKinds[e] == kind)
            {
                return true;
            }
        }
        return false;
    }

    [Fact]
    public void TaskGraphSource_EmitsTaskStepSubtaskBlocksExecutesAndProduced()
    {
        var brain = new BrainId("local");
        var sink = new GraphSink();
        sink.Node(brain.Value, GraphNodeKind.Brain, brain.Value);

        var parent = new AgentTask(
            "t-parent",
            "Ship v1",
            "Parent epic",
            "alice",
            TaskStatus.Running,
            [],
            DateTimeOffset.UtcNow);

        var child = new AgentTask(
            "t-child",
            "Build pipeline",
            "Child task",
            "alice",
            TaskStatus.Running,
            [
                new TaskStep(0, "Fetch", "Download data", "http.get", TaskStepStatus.Completed, null, "ok"),
                new TaskStep(1, "Transform", "Normalize", "data.clean", TaskStepStatus.Pending)
            ],
            DateTimeOffset.UtcNow,
            ParentId: "t-parent",
            Links: [new TaskLink("local/apps/notes", "produced")]);

        TaskGraphSource.Contribute(brain, sink, [parent, child]);
        var graph = GraphCompute.Build(sink, brain);

        var parentIdx = graph.IndexOf("local/tasks.task/t-parent");
        var childIdx = graph.IndexOf("local/tasks.task/t-child");
        var step0Idx = graph.IndexOf("local/tasks.task/t-child#step/0");
        var step1Idx = graph.IndexOf("local/tasks.task/t-child#step/1");
        var contract0Idx = graph.IndexOf("contract:http.get");
        var contract1Idx = graph.IndexOf("contract:data.clean");
        var appIdx = graph.IndexOf("local/apps/notes");

        Assert.True(parentIdx >= 0 && graph.Kinds[parentIdx] == GraphNodeKind.Task);
        Assert.True(childIdx >= 0 && graph.Kinds[childIdx] == GraphNodeKind.Task);
        Assert.True(step0Idx >= 0 && graph.Kinds[step0Idx] == GraphNodeKind.Step);
        Assert.True(step1Idx >= 0 && graph.Kinds[step1Idx] == GraphNodeKind.Step);
        Assert.True(contract0Idx >= 0 && graph.Kinds[contract0Idx] == GraphNodeKind.Contract);
        Assert.True(contract1Idx >= 0 && graph.Kinds[contract1Idx] == GraphNodeKind.Contract);
        Assert.True(appIdx >= 0 && graph.Kinds[appIdx] == GraphNodeKind.App);

        Assert.True(HasEdge(graph, parentIdx, childIdx, GraphEdgeKind.Subtask));
        Assert.True(HasEdge(graph, step0Idx, step1Idx, GraphEdgeKind.Blocks));
        Assert.True(HasEdge(graph, step0Idx, contract0Idx, GraphEdgeKind.Executes));
        Assert.True(HasEdge(graph, childIdx, appIdx, GraphEdgeKind.Produced));
    }

    [Fact]
    public async Task RunNeuron_And_RunGraphSource_EmitRunAndWorksOnWeights()
    {
        var os = TestOS.Local();
        os.Register<RunNeuron>();
        await using (os)
        {
            var brain = new BrainId("local");
            var run = os.Get<IRunNeuron>(new NeuronId(brain, "agent.run", "turn-1"));
            var started = await run.StartAsync("local/apps/creator", "t-1");
            Assert.Equal("Running", started.Status);
            Assert.Equal("local/apps/creator", started.Target);
            Assert.Equal("t-1", started.TaskId);

            var now = started.LastTurn;
            var recentSink = new GraphSink();
            recentSink.Node(brain.Value, GraphNodeKind.Brain, brain.Value);
            RunGraphSource.Contribute(brain, recentSink, [("local/agent.run/turn-1", started)], now);
            var recentGraph = GraphCompute.Build(recentSink, brain);
            var runIdx = recentGraph.IndexOf("local/agent.run/turn-1");
            Assert.True(runIdx >= 0 && recentGraph.Kinds[runIdx] == GraphNodeKind.Run);
            Assert.Contains(
                Enumerable.Range(recentGraph.RowStart[runIdx], recentGraph.RowStart[runIdx + 1] - recentGraph.RowStart[runIdx]),
                e => recentGraph.EdgeKinds[e] == GraphEdgeKind.WorksOn && Math.Abs(recentGraph.Weights[e] - 1f) < 0.01f);

            var stuckSink = new GraphSink();
            stuckSink.Node(brain.Value, GraphNodeKind.Brain, brain.Value);
            RunGraphSource.Contribute(brain, stuckSink, [("local/agent.run/turn-1", started)], now.AddMinutes(6));
            var stuckGraph = GraphCompute.Build(stuckSink, brain);
            var stuckRunIdx = stuckGraph.IndexOf("local/agent.run/turn-1");
            Assert.Contains(
                Enumerable.Range(stuckGraph.RowStart[stuckRunIdx], stuckGraph.RowStart[stuckRunIdx + 1] - stuckGraph.RowStart[stuckRunIdx]),
                e => stuckGraph.EdgeKinds[e] == GraphEdgeKind.WorksOn && stuckGraph.Weights[e] >= RunGraphSource.StuckRunThresholdMinutes);

            var completed = await run.CompleteAsync();
            Assert.Equal("Completed", completed.Status);
        }
    }

    [Fact]
    public async Task TaskExecutorNeuron_ExecutesImplementedCapability_SynthesizesMissingApp_AndStopsAtConfirmationGate()
    {
        var holder = new TestGraphServices();
        var os = TestOS.Local(o => o.Services = holder);
        holder.Observer = os;
        holder.Sources = [new StubCapabilityGraphSource()];

        os.Register<BrainGraphNeuron>();
        os.Register<TaskNeuron>();
        os.Register<TaskDirectoryNeuron>();
        os.Register<RunNeuron>();
        os.Register<TaskExecutorNeuron>();

        await using (os)
        {
            var brain = new BrainId("local");
            var engine = new TaskEngine(
                taskId => os.Get<ITaskNeuron>(new NeuronId(brain, "tasks.task", taskId)),
                () => os.Get<ITaskDirectoryNeuron>(new NeuronId(brain, "tasks.directory", "default")));

            // 1. Step 0 implemented by local/apps/notes -> executes and completes
            // 2. Step 1 not implemented -> synthesizes app and adds produced link
            // 3. Step 2 is confirmation gate ([Confirm]) -> stops at AwaitingConfirmation
            var created = await engine.CreateTaskAsync(new CreateTaskRequest(
                "Release workflow",
                "Test 3-path executor",
                "alice",
                [
                    new TaskStepDefinition("Save note", "Record release note", "notes.save"),
                    new TaskStepDefinition("Custom chart", "Build chart widget", "chart.render"),
                    new TaskStepDefinition("[Confirm] Merge PR", "Requires human approval", "github.merge")
                ]), TestContext.Current.CancellationToken);

            var executor = os.Get<ITaskExecutorNeuron>(new NeuronId(brain, "tasks.executor", "default"));
            var result = await executor.ExecuteNextStepAsync(created.TaskId);

            Assert.NotNull(result);
            Assert.Equal(TaskStatus.AwaitingConfirmation, result.Status);
            Assert.Equal(TaskStepStatus.Completed, result.Steps[0].Status);
            Assert.Contains("local/apps/notes", result.Steps[0].Output ?? string.Empty);

            Assert.Equal(TaskStepStatus.Completed, result.Steps[1].Status);
            Assert.Contains(result.Links ?? [], l => l.Kind == "produced" && l.TargetId == "local/apps/chart-render");

            Assert.Equal(TaskStepStatus.AwaitingConfirmation, result.Steps[2].Status);
        }
    }

    private sealed class TestGraphServices : IServiceProvider
    {
        public IBrainObserver? Observer { get; set; }
        public IGraphSource[] Sources { get; set; } = [];

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IBrainObserver)) { return Observer; }
            if (serviceType == typeof(IEnumerable<IGraphSource>)) { return Sources; }
            return null;
        }
    }

    private sealed class StubCapabilityGraphSource : IGraphSource
    {
        public Task Contribute(BrainId brain, GraphSink sink, CancellationToken cancellationToken)
        {
            var app = sink.Node($"{brain.Value}/apps/notes", GraphNodeKind.App, "notes");
            var contract = sink.Node("contract:notes.save", GraphNodeKind.Contract, "notes.save");
            sink.Edge(app, contract, GraphEdgeKind.Implements);
            return Task.CompletedTask;
        }
    }
}
