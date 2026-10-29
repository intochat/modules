using System.Reflection;
using System.Text.Json.Serialization;
using IntoChat.OS;
using IntoChat.OS.Framework.Modules;
using Orleans.Hosting;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Unit;

public interface IPlainCapability
{
    Task<string> Capability();
}

public interface IDiscoveryBase : IntoChat.OS.Modules.Tools.IToolSource, IPlainCapability
{
    Task<DiscoveryAddress> Inherited(CancellationToken cancellationToken);
    Task<Verdict> Check(CancellationToken cancellationToken = default);
}

public interface IDiscoveryFixture : IDiscoveryBase
{
    Task<DiscoveryResult> Read(DiscoveryRequest request, CancellationToken cancellationToken);
    Task<DiscoveryChoice> Choose(DiscoveryChoice choice, CancellationToken cancellationToken);
    Task<DiscoveryOuter<string>.Nested<int?>> Nested(Uri source, DiscoveryMode mode = DiscoveryMode.Preview);
    Task Defaults(char delimiter = ',', char quote = '\'', char slash = '\\', char newline = '\n', float fraction = 0.5f, decimal amount = 0.5m,
        float high = float.PositiveInfinity, float low = float.NegativeInfinity, float missing = float.NaN,
        double upper = double.PositiveInfinity, double lower = double.NegativeInfinity, double unknown = double.NaN);
}

public sealed class DiscoveryOuter<T>
{
    public sealed record Nested<U>(T Value, U Number);
}

[DigitalBrain.Contracts.DefaultNeuronType("fixture.hidden")]
public interface IHiddenFixture { Task Hidden(); }
[DigitalBrain.Contracts.DefaultNeuronType("fixture.expanding")]
public interface IExpandingFixture { Task<Expanding<int>> Read(); }
public sealed record Expanding<T>(Expanding<(T, T)>? Next);

public sealed record DiscoveryRequest(DiscoveryAddress[] Recipients, string? Cursor, DiscoveryMode Mode)
{
    public int Limit { get; init; } = 20;
}
public sealed record DiscoveryAddress(string Email, string? Name);
public sealed record DiscoveryResult(IReadOnlyList<DiscoveryAddress> Items, DiscoveryNode? Continuation);
public sealed record DiscoveryNode(string Value, DiscoveryNode? Next);
public enum DiscoveryMode { Preview, Create }

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(DiscoveryEmail), "email")]
public abstract record DiscoveryChoice;
public sealed record DiscoveryEmail(DiscoveryAddress Address) : DiscoveryChoice;
