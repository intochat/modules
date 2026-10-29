using System.ComponentModel;
using System.Reflection;
using IntoChat.OS.Modules.Microsoft.CSharp;
using ModelContextProtocol.Server;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Authoring;

public sealed record WriteCSharpFileRequest(string Source, string? Name = null, string? Purpose = null);
