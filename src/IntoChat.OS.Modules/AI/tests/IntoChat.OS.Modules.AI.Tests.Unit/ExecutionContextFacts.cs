using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DigitalBrain.Capabilities;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Modules.AI.Context;
using AIExecutionContext = IntoChat.OS.Modules.AI.Context.ExecutionContext;

namespace IntoChat.OS.Modules.AI.Tests.Unit;

public sealed class ExecutionContextFacts
{
    [Fact]
    public void ContextInitializesWithExplicitProperties()
    {
        var caller = CapabilityCallers.Of("alice", "acc-1", "brain-1", CallerKind.User);
        var env = new Dictionary<string, string> { ["MODEL"] = "gpt-5" };
        var slice = ContextSlice.Create("system", "You are an assistant", "instructions");

        var context = new AIExecutionContext("exec-123", caller, env, [slice], CancellationToken.None);

        Assert.Equal("exec-123", context.ExecutionId);
        Assert.Equal(caller, context.Caller);
        Assert.Equal("gpt-5", context.Environment["MODEL"]);
        Assert.Single(context.Slices);
        Assert.Equal("system", context.Slices[0].Name);
        Assert.Equal("You are an assistant", context.Slices[0].Content);
    }

    [Fact]
    public async Task WithSliceAsyncScopesSlicesOnlyDuringAction()
    {
        var caller = CapabilityCallers.Of("alice", "acc-1", "brain-1", CallerKind.User);
        var baseSlice = ContextSlice.Create("base", "base content");
        var context = new AIExecutionContext("exec-456", caller, null, [baseSlice]);

        Assert.Single(context.Slices);

        var dynamicSlice = ContextSlice.Create("dynamic", "dynamic content", "runtime");

        var insideCount = await context.WithSliceAsync(dynamicSlice, async () =>
        {
            await Task.Yield();
            Assert.Equal(2, context.Slices.Count);
            Assert.Equal("dynamic", context.Slices[1].Name);
            return context.Slices.Count;
        });

        Assert.Equal(2, insideCount);
        Assert.Single(context.Slices);
        Assert.Equal("base", context.Slices[0].Name);
    }

    [Fact]
    public void ExecutionContextProviderManagesCurrentAndPush()
    {
        var provider = new ExecutionContextProvider();
        var caller = CapabilityCallers.Of("bob", "acc-2", "brain-2", CallerKind.Agent);
        var custom = provider.Create("exec-custom", caller, cancellationToken: TestContext.Current.CancellationToken);

        var defaultContext = provider.Current;
        Assert.NotNull(defaultContext);
        Assert.False(string.IsNullOrWhiteSpace(defaultContext.ExecutionId));

        using (provider.PushContext(custom))
        {
            Assert.Equal("exec-custom", provider.Current.ExecutionId);
            Assert.Equal(caller, provider.Current.Caller);
        }

        Assert.Equal(defaultContext.ExecutionId, provider.Current.ExecutionId);
    }
}
