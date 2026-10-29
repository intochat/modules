using DigitalBrain.Runtime.Orleans;
using IntoChat.OS.Runtime;
using DigitalBrain.Spi;
using System.ComponentModel;
using System.Reflection;
using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;
using IntoChat.OS.Modules.Microsoft.CSharp;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
using ModelContextProtocol.Server;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Authoring;

// The contract discovery is registered by CSharpModule, so its absence means this host cannot run C# files.
public sealed class CSharpToolService(INeuronProxies brain, IBrainObserver kernel, CSharpContractDiscovery? contracts = null, CSharpScriptCheck? check = null)
{
    public static readonly (MethodInfo Method, string Name)[] Tools = [.. typeof(ScopedCSharpTools).GetMethods()
        .Select(method => (Method: method, Tool: method.GetCustomAttribute<McpServerToolAttribute>()))
        .Where(candidate => candidate.Tool is not null)
        .Select(candidate => (candidate.Method, candidate.Tool!.Name!))];

    public bool CanRun => contracts is not null;
    public CSharpContractDiscovery? Discovery => contracts;
    public CSharpScriptCheck? Check => check;

    public ScopedCSharpTools ForScope(string scope) => new(brain, kernel, contracts, scope, CanRun);
}
