using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.FileInput;

[Neuron(UIVocabulary.FileInputType)]
internal sealed class FileInputNeuron
    : UiNeuron<FileInputState>, IFileInput
{
    public Task Configure(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var next = Snapshot;
        next.Name = FullKey;
        next.Revision++;
        next.Label = label.Trim();
        return Save(next, new FileInputChanged(next.Name, next.Revision));
    }
    public async Task Capture(string name, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(content);
        if (name.Length > 255 || name.Any(char.IsControl)) { throw new ArgumentException("A filename has at most 255 printable characters.", nameof(name)); }
        if (content.Length > 32_000) { throw new ArgumentException("File text has at most 32000 characters.", nameof(content)); }
        var next = Snapshot;
        next.Name = FullKey;
        next.Revision++;
        var captured = new FileCaptured(next.Name, name, content);
        await Save(next, new FileInputChanged(next.Name, next.Revision), captured);
        await Dispatch(UiEvent.Of(captured));
    }
    public Task Target(string uploadUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadUrl);
        if (uploadUrl.Length > 2000 || uploadUrl.Any(char.IsControl)) { throw new ArgumentException("An upload address has at most 2000 printable characters.", nameof(uploadUrl)); }
        var next = Snapshot;
        next.Name = FullKey;
        next.Revision++;
        next.UploadUrl = uploadUrl.Trim();
        return Save(next, new FileInputChanged(next.Name, next.Revision));
    }
    public async Task Uploaded(string name, long bytes, string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        if (name.Length > 255 || name.Any(char.IsControl)) { throw new ArgumentException("A filename has at most 255 printable characters.", nameof(name)); }
        if (error is { Length: > 500 }) { throw new ArgumentException("An upload error has at most 500 characters.", nameof(error)); }
        var next = Snapshot;
        next.Name = FullKey;
        next.Revision++;
        var uploaded = new FileUploaded(next.Name, name, bytes, error);
        await Save(next, new FileInputChanged(next.Name, next.Revision), uploaded);
        await Dispatch(UiEvent.Of(uploaded));
    }
    public Task<FileInputState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
