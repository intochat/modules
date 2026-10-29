using DigitalBrain;
using System.Text.RegularExpressions;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.InfoBar.Signals;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.InfoBar;

[Neuron(UIVocabulary.InfoBarType)]
internal sealed class InfoBarNeuron
    : UiNeuron<InfoBarState>, IInfoBar
{
    public Task Show(string severity, string title, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(severity);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(body);
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Severity = severity.Trim();
        next.Title = title.Trim();
        next.Body = body;
        next.Visible = true;
        return Save(next, new InfoBarChanged(FullKey, true));
    }

    public Task Dismiss()
    {
        var next = Snapshot;
        next.Name = FullKey;
        next.Version++;
        next.Visible = false;
        return Save(next, new InfoBarChanged(FullKey, false), new InfoBarDismissed(FullKey));
    }

    [DigitalBrain.ReadOnlyCall] public Task<InfoBarState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
