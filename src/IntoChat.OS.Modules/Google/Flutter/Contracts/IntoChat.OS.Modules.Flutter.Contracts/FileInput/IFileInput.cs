using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.FileInput;

[NeuronAlias("fileinput"), DefaultNeuronType(UIVocabulary.FileInputType)]
public interface IFileInput
{
    Task Configure(string label);
    Task Capture(string name, string content);
    Task Target(string uploadUrl);
    Task Uploaded(string name, long bytes, string? error);
    [DigitalBrain.ReadOnlyCall] Task<FileInputState> Read();
}

[ExportableState]
public sealed class FileInputState
{
    public string Name { get; set; } = "";
    public long Revision { get; set; }
    public string Label { get; set; } = "";
    public string? UploadUrl { get; set; }
}

[Signal("ui.file-captured")]
public sealed record FileCaptured(string Name, string FileName, string Content) : DigitalBrain.Signal;

[Signal("ui.fileinput-changed")]
public sealed record FileInputChanged(string Name, long Revision) : DigitalBrain.Signal;

[Signal("ui.file-uploaded")]
public sealed record FileUploaded(string Name, string FileName, long Bytes, string? Error) : DigitalBrain.Signal;
