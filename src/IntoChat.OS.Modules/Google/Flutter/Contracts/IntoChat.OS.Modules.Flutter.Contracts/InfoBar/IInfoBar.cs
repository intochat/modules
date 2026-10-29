using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.InfoBar;

[NeuronAlias("infobar"), DefaultNeuronType(UIVocabulary.InfoBarType)]
public interface IInfoBar
{
    Task Show(string severity, string title, string body);
    Task Dismiss();
    [DigitalBrain.ReadOnlyCall] Task<InfoBarState> Read();
}

[ExportableState]
public sealed class InfoBarState
{
    public string Name { get; set; } = "";
    public int Version { get; set; }
    public string Severity { get; set; } = "info";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool Visible { get; set; }
}
