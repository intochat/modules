using System.ComponentModel;

namespace IntoChat.OS.Modules.Microsoft.Windows;

// Read and write files on one Windows PC, keyed {connectionKey}/files, confined to that machine's allowed roots.
// Browsing goes through the storage capability (ILegacyStorage) on the connection itself.
// A plain neuron (06-08).
[DigitalBrain.Contracts.NeuronAlias("windows.files"), DigitalBrain.Contracts.DefaultNeuronType("windows.files")]
public interface IFileSystem
{
    [Description("Read a text file on this Windows machine. path is absolute or relative to the first allowed root.")]
    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<string> Read(string path, CancellationToken cancellationToken = default);

    [Description("Write text to a file on this Windows machine, creating folders as needed; publishes FileWritten.")]
    [DigitalBrain.Interleaves]
    Task Write(string path, string content, CancellationToken cancellationToken = default);

    [Description("List the file and folder paths in a folder on this Windows machine.")]
    [DigitalBrain.ReadOnlyCall, DigitalBrain.Interleaves]
    Task<string[]> List(string path, CancellationToken cancellationToken = default);
}
