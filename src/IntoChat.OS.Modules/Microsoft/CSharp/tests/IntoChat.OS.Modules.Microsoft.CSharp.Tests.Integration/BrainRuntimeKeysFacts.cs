using IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

public sealed class BrainRuntimeKeysFacts
{
    [Fact]
    public void TheRuntimeKeyIsTheScopeThenRuntime() => Assert.Equal("acc-brain/runtime", BrainRuntimeKeys.For("acc-brain"));

    [Fact]
    public void AScopeWithASeparatorIsRefused() => Assert.Throws<ArgumentException>(() => BrainRuntimeKeys.For("a/b"));
}
