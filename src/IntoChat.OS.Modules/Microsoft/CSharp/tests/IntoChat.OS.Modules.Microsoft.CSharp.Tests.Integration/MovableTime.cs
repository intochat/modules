namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

internal sealed class MovableTime : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => Now;
}
