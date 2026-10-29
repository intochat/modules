using DigitalBrain;
using IntoChat.OS;
using IntoChat.OS.Modules.Flutter.Button;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Select;

[Neuron(UIVocabulary.SelectType)]
internal sealed class SelectNeuron
    : UiNeuron<SelectState>, ISelect
{
    public Task Set(string label, IReadOnlyList<SelectOption> options, string? selected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Count > 1000 || options.Any(option => option is null || option.Id is null || option.Id.Length > 200 || string.IsNullOrWhiteSpace(option.Label))
            || options.Select(option => option.Id).Distinct(StringComparer.Ordinal).Count() != options.Count)
        { throw new ArgumentException("Select options must have unique bounded IDs and nonempty labels.", nameof(options)); }
        if (selected is not null && !options.Any(option => option.Id == selected)) { throw new ArgumentException("Selected option is missing.", nameof(selected)); }
        return Save(new() { Name = FullKey, Revision = Snapshot.Revision + 1, Label = label.Trim(), Options = options.ToArray(), Selected = selected, Visual = Snapshot.Visual },
            new SelectChanged(FullKey, selected ?? ""));
    }
    public Task SetVisual(ControlVisual visual)
    {
        ControlVisualValidation.Check(visual);
        var next = Snapshot;
        next.Visual = visual;
        next.Revision++;
        return Save(next, new SelectChanged(FullKey, ""));
    }
    public async Task Choose(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!Snapshot.Options.Any(option => option.Id == value && option.Enabled)) { throw new ArgumentException("Choose an enabled option.", nameof(value)); }
        var signal = new SelectChanged(FullKey, value);
        var next = Snapshot;
        next.Selected = value;
        next.Revision++;
        await Save(next, signal);
        await Dispatch(UiEvent.Of(signal));
    }
    public Task<SelectState> Read() { Snapshot.Name = FullKey; return Task.FromResult(Snapshot); }
}
