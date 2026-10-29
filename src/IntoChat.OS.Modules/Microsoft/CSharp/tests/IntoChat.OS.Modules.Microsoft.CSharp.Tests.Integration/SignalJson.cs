using System.Text.Json;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Integration;

// A signal's JSON also carries its publisher, which differs per neuron; facts compare the payload only.
internal static class SignalJson
{
    public static int Number(string json) => JsonDocument.Parse(json).RootElement.GetProperty("number").GetInt32();
    public static Guid RequestId(string json) => JsonDocument.Parse(json).RootElement.GetProperty("requestId").GetGuid();
}
