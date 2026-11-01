using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DigitalBrain.Capabilities;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Modules.AI.Context;
using Xunit;
using AIExecutionContext = IntoChat.OS.Modules.AI.Context.ExecutionContext;

namespace IntoChat.OS.Modules.AI.Tests.Unit;

public sealed class ExecutionContextChallengeFacts
{
    [Fact]
    public async Task DeepNestedSliceStackingPreservesOrderAndUnwindsCleanly()
    {
        var context = new AIExecutionContext("exec-deep", null);
        const int depth = 50;

        async Task Recurse(int currentDepth)
        {
            if (currentDepth > depth)
            {
                Assert.Equal(depth, context.Slices.Count);
                for (var i = 0; i < depth; i++)
                {
                    Assert.Equal($"slice-{i + 1}", context.Slices[i].Name);
                }
                return;
            }

            var slice = ContextSlice.Create($"slice-{currentDepth}", $"content-{currentDepth}");
            await context.WithSliceAsync(slice, async () =>
            {
                Assert.Equal(currentDepth, context.Slices.Count);
                Assert.Equal($"slice-{currentDepth}", context.Slices[^1].Name);
                await Task.Yield();
                await Recurse(currentDepth + 1);
                Assert.Equal(currentDepth, context.Slices.Count);
                return true;
            });
        }

        await Recurse(1);
        Assert.Empty(context.Slices);
    }

    [Fact]
    public async Task WithSliceAsyncUnwindsProperlyWhenActionThrows()
    {
        var baseSlice = ContextSlice.Create("base", "content");
        var context = new AIExecutionContext("exec-throw", null, initialSlices: [baseSlice]);
        Assert.Single(context.Slices);

        var leakedSlice = ContextSlice.Create("faulty", "evil");
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await context.WithSliceAsync(leakedSlice, async () =>
            {
                await Task.Yield();
                Assert.Equal(2, context.Slices.Count);
                throw new InvalidOperationException("Simulation failure");
#pragma warning disable CS0162 // Unreachable code detected
                return true;
#pragma warning restore CS0162
            });
        });

        Assert.Single(context.Slices);
        Assert.Equal("base", context.Slices[0].Name);
    }

    [Fact]
    public async Task SiblingConcurrentTasksOnSameExecutionContextDoNotLeakSlices()
    {
        var baseSlice = ContextSlice.Create("root", "immutable");
        var sharedContext = new AIExecutionContext("exec-concurrent", null, initialSlices: [baseSlice]);
        const int workerCount = 50;

        var tasks = Enumerable.Range(0, workerCount).Select(async workerId =>
        {
            var workerSlice = ContextSlice.Create($"worker-{workerId}", $"data-{workerId}");
            await Task.Yield();

            await sharedContext.WithSliceAsync(workerSlice, async () =>
            {
                await Task.Delay(Random.Shared.Next(1, 10));

                // Verify this worker sees root + workerSlice, and NO other worker's slice
                var currentSlices = sharedContext.Slices;
                Assert.Equal(2, currentSlices.Count);
                Assert.Equal("root", currentSlices[0].Name);
                Assert.Equal($"worker-{workerId}", currentSlices[1].Name);

                // Nested stacking inside worker
                var innerSlice = ContextSlice.Create($"worker-{workerId}-sub", "sub");
                await sharedContext.WithSliceAsync(innerSlice, async () =>
                {
                    await Task.Yield();
                    var inner = sharedContext.Slices;
                    Assert.Equal(3, inner.Count);
                    Assert.Equal($"worker-{workerId}-sub", inner[2].Name);
                    return true;
                });

                return true;
            });

            // Post-WithSlice inside worker task: should revert to root
            Assert.Single(sharedContext.Slices);
            Assert.Equal("root", sharedContext.Slices[0].Name);
        });

        await Task.WhenAll(tasks);

        // Outside all tasks, root context must remain untouched
        Assert.Single(sharedContext.Slices);
        Assert.Equal("root", sharedContext.Slices[0].Name);
    }

    [Fact]
    public async Task SliceStackingSurvivesThreadPoolThreadHops()
    {
        var context = new AIExecutionContext("exec-hops", null);
        var slice = ContextSlice.Create("hopped", "thread-switch");

        await context.WithSliceAsync(slice, async () =>
        {
            Assert.Single(context.Slices);
            await Task.Delay(15).ConfigureAwait(false);
            Assert.Single(context.Slices);
            Assert.Equal("hopped", context.Slices[0].Name);

            await Task.Yield();
            Assert.Single(context.Slices);
            Assert.Equal("hopped", context.Slices[0].Name);
            return 42;
        });

        Assert.Empty(context.Slices);
    }

    [Fact]
    public async Task ContextRequiresNonNullArgumentsForSliceScoping()
    {
        var context = new AIExecutionContext("exec-nulls", null);
        var slice = ContextSlice.Create("valid", "valid");

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await context.WithSliceAsync<int>(null!, () => Task.FromResult(1));
        });

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await context.WithSliceAsync<int>(slice, null!);
        });
    }

    [Fact]
    public void ExecutionContextProviderLeavesCallerNullWhenNoProvenanceExists()
    {
        var provider = new ExecutionContextProvider();
        var context = provider.Create(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(context.Caller);
    }
}
