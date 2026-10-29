using DigitalBrain;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Files.Signals;

// Published on every durable change of a file: source, settings, run, trigger or subscriptions.
[Signal("microsoft.csharp.file-changed")]
public sealed record CSharpFileChanged(string FileId) : DigitalBrain.Signal;
