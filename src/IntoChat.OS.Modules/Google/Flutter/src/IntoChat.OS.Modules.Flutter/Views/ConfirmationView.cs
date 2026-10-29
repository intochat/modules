using DigitalBrain.UI.Confirmation;
using IntoChat.OS.Confirmation;
using IntoChat.OS.Framework;
using IntoChat.OS.Framework.Modules;

namespace IntoChat.OS.Modules.Flutter.Views;

// A host-owned leaf: its buttons go straight to the runtime inbox on the viewer's HTTP session.
internal static class ConfirmationView
{
    public static UiView Render(ConfirmationInbox inbox)
    {
        var human = CallerContextStamper.TryGet(out var caller) && CallerContextStamper.IsTrusted(caller)
            && caller.Kind == CallerKind.User && caller.StampedBy == TrustedEdge.AuthenticatedHttp
            && caller.AppId is null && caller.OnBehalfOf is null && !AppPrincipal.IsApp(caller.PrincipalId)
            && !CallerContextStamper.IsModelOriginated(caller);
        var cards = inbox.Pending.Select((request, index) =>
        {
            var plan = request.Plan;
            var card = ConfirmView.For(request.PlanId, plan.Title, plan.Body, plan.ApproveLabel, plan.DenyLabel, plan.Expiry, human);
            var key = "confirmation-" + index;
            return Ui.Card(key, card.Title, card.Body, card.Children.Select((node, child) => node switch
            {
                DigitalBrain.UI.Text text => Ui.Text(key + "-" + child, text.Value),
                DigitalBrain.UI.Button button => Ui.Button(key + "-" + child, button.Label,
                    button.Action.Operation + ":" + Uri.EscapeDataString(button.Action.Arguments[0]) + ":" + button.Action.Arguments[1],
                    button.Enabled, button.Tone == DigitalBrain.UI.ButtonTone.Accent ? UiStyle.Primary : UiStyle.Secondary),
                _ => throw new InvalidOperationException("Unsupported confirmation card node."),
            }).ToArray());
        }).ToArray();
        return new(inbox.Revision, Math.Max(1, inbox.Revision), Ui.Column("confirmations", cards));
    }

    public static (string PlanId, bool Approve) Answer(string? action)
    {
        var parts = action?.Split(':');
        if (parts is not [ConfirmView.ResolveOperation, { Length: > 0 } id, "true" or "false"])
        { throw new ArgumentException("Invalid confirmation answer."); }
        var decoded = Uri.UnescapeDataString(id);
        if (decoded.Length > 128 || Uri.EscapeDataString(decoded) != id)
        { throw new ArgumentException("Invalid confirmation plan id."); }
        return (decoded, parts[2] == "true");
    }
}
