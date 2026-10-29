using DigitalBrain.UI;
using IntoChat.OS.Client;
using IntoChat.OS.Modules.Flutter.Views;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

// The presenter an [App] class is built with: what it presents is published to the app's view neurons, main at "{app}/view" and side at
// "{app}/side", one publication after another. Present returns at once; a publication that fails is written to the run log.
internal sealed class ViewPresenter(IntoChatOSConnection brain, string appKey, long generation) : IPresenter
{
    private readonly Lock _gate = new();
    private readonly ViewPublisher _main = new(brain.Get<IView>(appKey + "/view"), generation);
    private readonly ViewPublisher _side = new(brain.Get<IView>(appKey + "/side"), generation);
    private Task _tail = Task.CompletedTask;

    public void Present(View? side, View? main)
    {
        if (side is not null) { Enqueue(_side, side); }
        if (main is not null) { Enqueue(_main, main); }
    }

    public void Toast(Toast toast) => AppLog.Log("toast: " + toast.Message);

    // Completes once everything presented so far has been published.
    public Task DrainAsync()
    {
        lock (_gate) { return _tail; }
    }

    private void Enqueue(ViewPublisher publisher, View view)
    {
        var tree = Map(view);
        lock (_gate) { _tail = Publish(_tail, publisher, tree); }
    }

    private static async Task Publish(Task previous, ViewPublisher publisher, UiNode tree)
    {
        await previous.ConfigureAwait(false);
        try { await publisher.Publish(tree).ConfigureAwait(false); }
        catch (Exception error) when (error is not OperationCanceledException) { AppLog.Log("The view could not be published: " + error.Message); }
    }

    // The framework's renderer-neutral nodes as the shell's view tree. A button's action is its operation and arguments, as the host's
    // own views write them; a text field shows as its label, as the shell's tree has no editable field.
    internal static UiNode Map(View view) => Ui.Column("view", [Ui.Text("title", view.Title), Map("root", view.Root)]);

    private static UiNode Map(string key, Node node) => node switch
    {
        Column column => Ui.Column(key, [.. column.Children.Select((child, index) => Map(key + "." + index, child))]),
        Collection collection => Ui.List(key, [.. collection.Entries.Select(entry => Ui.Card(entry.Id, entry.Title, entry.Subtitle))]),
        Text text => Ui.Text(key, text.Value),
        TextField field => Ui.Text(key, field.Value ?? field.Placeholder ?? field.Name),
        DigitalBrain.UI.Toast toast => Ui.Toast(key, toast.Message),
        Card card => Ui.Card(key, card.Title, card.Body, [.. card.Children.Select((child, index) => Map(key + "." + index, child))]),
        Button button => Ui.Button(key, button.Label, button.Action.Operation + ":" + string.Join(':', button.Action.Arguments.Select(Uri.EscapeDataString)),
            button.Enabled, button.Tone == ButtonTone.Accent ? UiStyle.Primary : UiStyle.Secondary),
        _ => throw new InvalidOperationException("Unsupported view node " + node.GetType().Name + "."),
    };
}
