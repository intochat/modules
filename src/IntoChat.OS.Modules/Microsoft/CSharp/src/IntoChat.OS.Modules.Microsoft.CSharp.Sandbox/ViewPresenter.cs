using System.Globalization;
using System.Text.Json;
using DigitalBrain.Contracts;
using DigitalBrain.UI;
using IntoChat.OS.Client;
using IntoChat.OS.Modules.Flutter;
using IntoChat.OS.Modules.Flutter.Views;
using Ui = IntoChat.OS.Modules.Flutter.Views.Ui;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

// The presenter an [App] class is built with: what it presents is published to the app's view neurons, main at "{app}/view" and side at
// "{app}/side", one publication after another. Present returns at once; a publication that fails is written to the run log.
internal sealed class ViewPresenter(IntoChatOSConnection brain, string appKey, long generation) : IPresenter
{
    private static readonly HashSet<string> SupportedReferenceKinds = new(StringComparer.Ordinal)
    {
        "confirmation", "viewport", "chat", "composer", "text", "button", "textfield", "select",
        "fileinput", "imagecanvas", "form", "table", "chart", "voiceinput", "webbrowser",
        "toggle", "progress", "infobar", "image", "entrycontrol", "preference",
    };

    private readonly Lock _gate = new();
    private readonly ViewPublisher _main = new(brain.Get<IView>(appKey + "/view"), generation);
    private readonly ViewPublisher _side = new(brain.Get<IView>(appKey + "/side"), generation);
    private readonly string _brainId = appKey.Contains('/', StringComparison.Ordinal) ? appKey[..appKey.IndexOf('/', StringComparison.Ordinal)] : "owner";
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
        var tree = Map(view, _brainId);
        lock (_gate) { _tail = Publish(_tail, publisher, tree); }
    }

    private static async Task Publish(Task previous, ViewPublisher publisher, UiNode tree)
    {
        await previous.ConfigureAwait(false);
        try { await publisher.Publish(tree).ConfigureAwait(false); }
        catch (Exception error) when (error is not OperationCanceledException) { AppLog.Log("The view could not be published: " + error.Message); }
    }

    // The framework's renderer-neutral nodes as the shell's view tree.
    internal static UiNode Map(View view, string? brainId = null)
        => Ui.Column("view", [Ui.Text("title", view.Title), Map("root", view.Root, brainId)]);

    internal static UiNode Map(string key, Node node, string? brainId = null) => node switch
    {
        Column column => Ui.Column(
            key,
            [.. column.Children.Select((child, index) => Map($"{key}.{index}", child, brainId))],
            Math.Max(0, column.Gap),
            column.Extents is { Count: > 0 } colExtents && colExtents.Count == column.Children.Count ? [.. colExtents] : null),

        Row row => Ui.Row(
            key,
            [.. row.Children.Select((child, index) => Map($"{key}.{index}", child, brainId))],
            Math.Max(0, row.Gap),
            row.Extents is { Count: > 0 } rowExtents && rowExtents.Count == row.Children.Count ? [.. rowExtents] : null),

        Grid grid => Ui.Card(
            key,
            string.Empty,
            JsonSerializer.Serialize(new { _ui = "grid", columns = Math.Max(1, grid.Columns), gap = Math.Max(0, grid.Gap) }),
            [.. grid.Children.Select((child, index) => Map($"{key}.{index}", child, brainId))]),

        SplitPane split => Ui.Row(
            key,
            [Map($"{key}.0", split.Leading, brainId), Map($"{key}.1", split.Trailing, brainId)],
            Math.Max(0, split.Gap),
            [Math.Max(0, split.LeadingExtent), 0]),

        Surface surface => Ui.Card(
            key,
            surface.Title ?? string.Empty,
            JsonSerializer.Serialize(new
            {
                _ui = "surface",
                kind = (surface.Kind ?? SurfaceKind.Acrylic).ToLowerInvariant(),
                subtitle = surface.Subtitle,
                gap = 12.0,
                padding = Math.Max(0, surface.Padding),
            }),
            [.. surface.Children.Select((child, index) => Map($"{key}.{index}", child, brainId))]),

        Tabs tabs => MapTabs(key, tabs),

        Divider divider => Ui.Divider(key, divider.Label),

        Spacer spacer => Ui.Card(
            key,
            string.Empty,
            JsonSerializer.Serialize(new { _ui = "spacer", size = Math.Max(0, spacer.Size) })),

        Collection collection => Ui.List(
            key,
            [.. collection.Entries.Select((entry, index) => Ui.Card($"{key}.entry.{index}.{entry.Id}", entry.Title, entry.Subtitle))]),

        Heading heading => new UiNode(UiNodeKind.Text, key)
        {
            Text = heading.Value,
            Detail = JsonSerializer.Serialize(new { _ui = "heading", level = heading.Level, subtitle = heading.Subtitle }),
        },

        Text text => (string.IsNullOrEmpty(text.Tone) || text.Tone == "neutral" || text.Tone == "default") && !text.Tabular && !text.Mono && text.Size is null
            ? Ui.Text(key, text.Value)
            : new UiNode(UiNodeKind.Text, key)
            {
                Text = text.Value,
                Detail = JsonSerializer.Serialize(new
                {
                    _ui = "text",
                    tone = text.Tone.ToLowerInvariant(),
                    tabular = text.Tabular,
                    mono = text.Mono,
                    size = text.Size,
                }),
            },

        Badge badge => new UiNode(UiNodeKind.Badge, key)
        {
            Text = NonEmpty(badge.Label, "•"),
            Tone = MapToneString(badge.Tone),
            Icon = SanitizeIcon(badge.Icon),
        },

        TagPill pill => new UiNode(UiNodeKind.Badge, key)
        {
            Text = NonEmpty(pill.Label, "•"),
            Tone = MapToneString(pill.Tone),
            Icon = SanitizeIcon(pill.Icon),
            Detail = JsonSerializer.Serialize(new { _ui = "tagpill", tone = pill.Tone.ToLowerInvariant() }),
        },

        Button button => new UiNode(UiNodeKind.Button, key)
        {
            Text = NonEmpty(button.Label, NonEmpty(button.Action.Operation, "Action")),
            Action = FormatAction(button.Action),
            Enabled = button.Enabled,
            Style = MapButtonStyle(button),
            Icon = SanitizeIcon(button.Icon),
            Detail = button.Small ? "small" : null,
        },

        TextField field => Ui.Card(
            key,
            field.Value ?? field.Placeholder ?? field.Name,
            JsonSerializer.Serialize(new
            {
                _ui = "textfield",
                name = field.Name,
                value = field.Value,
                placeholder = field.Placeholder,
                secret = false,
            }),
            field.OnSubmit is { } submitAction
                ? [Ui.Button($"{key}.submit", NonEmpty(field.Name, "Submit"), FormatAction(submitAction), style: UiStyle.Primary)]
                : []),

        Switch sw => Ui.Card(
            key,
            sw.Label,
            JsonSerializer.Serialize(new
            {
                _ui = "switch",
                label = sw.Label,
                value = sw.IsOn,
                enabled = sw.Action is not null,
                locked = sw.Locked,
            }),
            sw.Action is { } swAction
                ? [Ui.Button($"{key}.toggle", NonEmpty(sw.Label, "Toggle"), FormatAction(swAction), !sw.Locked)]
                : []),

        Select select => Ui.Card(
            key,
            select.Label,
            JsonSerializer.Serialize(new
            {
                _ui = "select",
                label = select.Label,
                selectedId = select.Selected,
                enabled = true,
                options = select.Options.Select(o => new { id = o.Id, label = o.Label }),
            }),
            [.. select.Options.Select((opt, index) => Ui.Button(
                $"{key}.opt.{index}",
                NonEmpty(opt.Label, opt.Id),
                FormatAction(opt.Action ?? new ButtonAction("app", "select", [opt.Id]))))]),

        Progress progress => Ui.Card(
            key,
            progress.Label,
            JsonSerializer.Serialize(new
            {
                _ui = "progress",
                value = progress.Value,
                label = progress.Label,
                detail = progress.Detail,
                indeterminate = false,
            })),

        InfoBar info => Ui.Card(
            key,
            info.Title,
            JsonSerializer.Serialize(new
            {
                _ui = "infobar",
                title = info.Title,
                body = info.Message,
                tone = info.Tone.ToLowerInvariant(),
            }),
            info.Action is { } infoBtn ? [Map($"{key}.action", infoBtn, brainId)] : []),

        DigitalBrain.UI.Toast toast => Ui.Toast(key, toast.Message),

        Card card => Ui.Card(
            key,
            card.Title,
            card.Body,
            [.. card.Children.Select((child, index) => Map($"{key}.{index}", child, brainId))]),

        DataTable table => MapDataTable(key, table),

        Chart chart => MapChart(key, chart),

        ApprovalCardNode approval => MapApprovalCard(key, approval),

        ChoiceCardNode choice => MapChoiceCard(key, choice),

        DraftCardNode draft => MapDraftCard(key, draft),

        RecordCardNode record => MapRecordCard(key, record),

        SecretCardNode secret => MapSecretCard(key, secret),

        JobCardNode job => MapJobCard(key, job),

        BrainSceneNode scene => MapBrainScene(key, scene),

        NeuronRef neuronRef => MapNeuronRef(key, neuronRef, brainId),

        AppShellNode shell => MapAppShell(key, shell, brainId),

        NavItemNode nav => MapNavItem(key, nav),

        StatCardNode stat => MapStatCard(key, stat),

        _ => throw new InvalidOperationException("Unsupported view node " + node.GetType().Name + "."),
    };

    private static UiNode MapAppShell(string key, AppShellNode shell, string? brainId)
    {
        var shellKey = NonEmpty(shell.Key, key);
        var children = new List<UiNode>();
        if (shell.Sidebar is not null) { children.Add(Map($"{shellKey}.sidebar", shell.Sidebar, brainId)); }
        if (shell.Toolbar is not null) { children.Add(Map($"{shellKey}.toolbar", shell.Toolbar, brainId)); }
        children.Add(Map($"{shellKey}.body", shell.Body, brainId));
        if (shell.Inspector is not null) { children.Add(Map($"{shellKey}.inspector", shell.Inspector, brainId)); }

        return Ui.Card(
            shellKey,
            string.Empty,
            JsonSerializer.Serialize(new
            {
                _ui = "appshell",
                hasSidebar = shell.Sidebar is not null,
                hasToolbar = shell.Toolbar is not null,
                hasInspector = shell.Inspector is not null,
            }),
            [.. children]);
    }

    private static UiNode MapNavItem(string key, NavItemNode nav)
    {
        var navKey = NonEmpty(nav.Key, key);
        return Ui.Card(
            navKey,
            nav.Label,
            JsonSerializer.Serialize(new
            {
                _ui = "navitem",
                label = nav.Label,
                icon = SanitizeIcon(nav.Icon) ?? nav.Icon,
                selected = nav.Selected,
                badge = nav.Badge,
            }),
            [Ui.Button($"{navKey}.act", NonEmpty(nav.Label, "Select"), NonEmpty(nav.Action, "noop"), style: nav.Selected ? UiStyle.Primary : UiStyle.Ghost)]);
    }

    private static UiNode MapStatCard(string key, StatCardNode stat) => Ui.Card(
        NonEmpty(stat.Key, key),
        stat.Label,
        JsonSerializer.Serialize(new
        {
            _ui = "statcard",
            label = stat.Label,
            value = stat.Value,
            delta = stat.Delta,
            tone = stat.Tone.ToString().ToLowerInvariant(),
            icon = SanitizeIcon(stat.Icon) ?? stat.Icon,
        }));

    private static UiNode MapTabs(string key, Tabs tabs)
    {
        if (tabs.Items.Count == 0)
        {
            return Ui.Column(key, []);
        }

        var activeIndex = -1;
        for (var i = 0; i < tabs.Items.Count; i++)
        {
            if (string.Equals(tabs.Items[i].Id, tabs.SelectedId, StringComparison.Ordinal))
            {
                activeIndex = i;
                break;
            }
        }
        if (activeIndex < 0) { activeIndex = 0; }

        var tabButtons = tabs.Items
            .Select((item, index) => Ui.Tab(
                $"{key}.tab.{index}",
                NonEmpty(item.Label, item.Id),
                FormatAction(item.Action),
                index == activeIndex))
            .ToArray();

        return Ui.Tabs(key, tabButtons);
    }

    private static UiNode MapDataTable(string key, DataTable table)
    {
        var buttons = new List<UiNode>();
        if (!string.IsNullOrWhiteSpace(table.SourceNeuronUri))
        {
            buttons.Add(Ui.CopyButton($"{key}.copy", "Copy URI", SanitizeCopyText(table.SourceNeuronUri!)));
        }

        return Ui.Card(
            key,
            table.Title,
            JsonSerializer.Serialize(new
            {
                _ui = "datatable",
                title = table.Title,
                columns = table.Columns,
                rows = table.Rows,
                sourceNeuronUri = table.SourceNeuronUri,
                from = table.From,
            }),
            [.. buttons]);
    }

    private static UiNode MapChart(string key, Chart chart)
    {
        var buttons = new List<UiNode>();
        if (!string.IsNullOrWhiteSpace(chart.SourceNeuronUri))
        {
            buttons.Add(Ui.CopyButton($"{key}.copy", "Copy URI", SanitizeCopyText(chart.SourceNeuronUri!)));
        }

        return Ui.Card(
            key,
            chart.Title,
            JsonSerializer.Serialize(new
            {
                _ui = "chart",
                kind = chart.Kind,
                title = chart.Title,
                subtitle = chart.From,
                unit = chart.Prefix,
                seriesName = chart.SeriesName,
                compareName = chart.CompareName,
                sourceNeuronUri = chart.SourceNeuronUri,
                points = chart.Points.Select((p, idx) => new { id = $"pt-{idx}", label = p.Label, value = p.Value }),
                comparePoints = chart.ComparePoints?.Select((v, idx) => new { id = $"cmp-{idx}", label = idx < chart.Points.Count ? chart.Points[idx].Label : $"pt-{idx}", value = v }),
            }),
            [.. buttons]);
    }

    private static UiNode MapApprovalCard(string key, ApprovalCardNode approval)
    {
        var buttons = new List<UiNode>();
        if (approval.OnApprove is { } approveAction)
        {
            buttons.Add(Ui.Button($"{key}.approve", "Approve", FormatAction(approveAction), style: UiStyle.Primary));
        }
        if (approval.OnReject is { } rejectAction)
        {
            buttons.Add(Ui.Button($"{key}.reject", "Deny", FormatAction(rejectAction), style: UiStyle.Secondary));
        }
        if (!string.IsNullOrWhiteSpace(approval.SourceNeuronUri))
        {
            buttons.Add(Ui.CopyButton($"{key}.copy", "Copy URI", SanitizeCopyText(approval.SourceNeuronUri!)));
        }

        return Ui.Card(
            key,
            approval.Title,
            JsonSerializer.Serialize(new
            {
                _ui = "approval",
                id = approval.Id,
                verb = approval.Title,
                target = string.Empty,
                rows = approval.Rows.Select(r => new { key = r.Label, value = r.NewValue, oldValue = r.OldValue, amount = r.Amount }),
                sourceApp = approval.SystemName,
                sourceNeuronUri = approval.SourceNeuronUri,
                scopeUri = approval.Summary,
                state = approval.Status,
                approveLabel = "Approve",
                rejectLabel = "Deny",
                rememberHint = approval.Summary,
            }),
            [.. buttons]);
    }

    private static UiNode MapChoiceCard(string key, ChoiceCardNode choice)
    {
        var buttons = choice.Options
            .Select((opt, index) => Ui.Button(
                $"{key}.opt.{index}",
                NonEmpty(opt.Title, $"Option {index + 1}"),
                FormatAction(opt.Action ?? new ButtonAction("app", "choose", [index.ToString(CultureInfo.InvariantCulture)]))))
            .ToArray();

        return Ui.Card(
            key,
            choice.Question,
            JsonSerializer.Serialize(new
            {
                _ui = "choice",
                question = choice.Question,
                selectedIndex = choice.PickedIndex,
                sourceApp = "Ino",
                allowOther = true,
                options = choice.Options.Select(o => new { label = o.Title, note = o.Detail }),
            }),
            buttons);
    }

    private static UiNode MapDraftCard(string key, DraftCardNode draft)
    {
        var buttons = new List<UiNode>();
        if (draft.OnSend is { } sendAction)
        {
            buttons.Add(Ui.Button($"{key}.send", "Send", FormatAction(sendAction), style: UiStyle.Primary));
        }
        if (draft.OnDiscard is { } cancelAction)
        {
            buttons.Add(Ui.Button($"{key}.cancel", "Discard", FormatAction(cancelAction), style: UiStyle.Ghost));
        }

        return Ui.Card(
            key,
            draft.Subject ?? draft.To,
            JsonSerializer.Serialize(new
            {
                _ui = "draft",
                channel = draft.Channel,
                recipient = draft.To,
                subject = draft.Subject,
                body = draft.Body,
                sent = string.Equals(draft.Status, "sent", StringComparison.OrdinalIgnoreCase),
                sendLabel = "Send",
            }),
            [.. buttons]);
    }

    private static UiNode MapRecordCard(string key, RecordCardNode record)
    {
        var buttons = new List<UiNode>();
        if (record.OnOpen is { } openAction)
        {
            buttons.Add(Ui.Button($"{key}.open", NonEmpty(record.LinkLabel, "Open"), FormatAction(openAction), style: UiStyle.Secondary));
        }

        return Ui.Card(
            key,
            record.Name,
            JsonSerializer.Serialize(new
            {
                _ui = "record",
                kind = record.ObjectLabel,
                title = record.Name,
                subtitle = record.Subtitle,
                status = "ready",
                statusTone = "accent",
                sourceApp = record.ObjectLabel,
                fields = record.Fields.Select(f => new { key = f.Label, value = f.Value }),
            }),
            [.. buttons]);
    }

    private static UiNode MapSecretCard(string key, SecretCardNode secret)
    {
        var buttons = new List<UiNode>();
        if (secret.OnEnter is { } saveAction)
        {
            buttons.Add(Ui.Button($"{key}.save", "Save to vault", FormatAction(saveAction), style: UiStyle.Primary));
        }
        if (secret.OnSkip is { } skipAction)
        {
            buttons.Add(Ui.Button($"{key}.skip", "Skip", FormatAction(skipAction), style: UiStyle.Ghost));
        }

        return Ui.Card(
            key,
            secret.Name,
            JsonSerializer.Serialize(new
            {
                _ui = "secret",
                service = secret.Name,
                keyName = secret.SavedIn,
                hint = secret.Why,
                saved = string.Equals(secret.Status, "saved", StringComparison.OrdinalIgnoreCase),
            }),
            [.. buttons]);
    }

    private static UiNode MapJobCard(string key, JobCardNode job)
    {
        var buttons = new List<UiNode>();
        if (job.OnCancel is { } cancelAction)
        {
            buttons.Add(Ui.Button($"{key}.cancel", "Cancel", FormatAction(cancelAction), style: UiStyle.Ghost));
        }

        var currentStep = 0;
        for (var i = 0; i < job.Steps.Count; i++)
        {
            if (string.Equals(job.Steps[i].Status, "running", StringComparison.OrdinalIgnoreCase))
            {
                currentStep = i;
                break;
            }
        }

        return Ui.Card(
            key,
            job.Title,
            JsonSerializer.Serialize(new
            {
                _ui = "job",
                title = job.Title,
                currentStep,
                progress = job.Progress,
                eta = job.ResultText,
                done = string.Equals(job.Status, "done", StringComparison.OrdinalIgnoreCase),
                steps = job.Steps.Select(s => new { label = s.Label, detail = s.Detail }),
            }),
            [.. buttons]);
    }

    private static UiNode MapBrainScene(string key, BrainSceneNode scene)
    {
        var buttons = scene.Nodes
            .Select((n, index) => Ui.Button(
                $"{key}.node.{index}",
                NonEmpty(n.Label, NonEmpty(n.Id, $"Node {index + 1}")),
                FormatAction(n.OnSelect ?? new ButtonAction("app", "inspect", [n.Id]))))
            .ToArray();

        return Ui.Card(
            key,
            "Brain Scene 3D",
            JsonSerializer.Serialize(new
            {
                _ui = "brain3d",
                selectedId = scene.SelectedId,
                height = 340.0,
                interactive = true,
                showHud = true,
                nodes = scene.Nodes.Select(n => new
                {
                    id = n.Id,
                    label = n.Label,
                    category = n.Kind,
                    subtitle = n.Uri,
                    status = n.Status,
                    x = n.X,
                    y = n.Y,
                    z = n.Z,
                }),
                synapses = scene.Synapses.Select(s => new
                {
                    fromId = s.FromId,
                    toId = s.ToId,
                    active = s.Active,
                    signalType = s.SignalLabel ?? "Signal",
                }),
            }),
            buttons);
    }

    private static UiNode MapNeuronRef(string key, NeuronRef neuronRef, string? brainId)
    {
        var children = new List<UiNode>
        {
            Ui.CopyButton($"{key}.copy", "Copy URI", SanitizeCopyText(neuronRef.NeuronUri)),
        };

        if (!string.IsNullOrWhiteSpace(brainId)
            && SupportedReferenceKinds.Contains(neuronRef.Kind)
            && TryResolveBrainKey(neuronRef.NeuronUri, brainId!, out var resolvedKey))
        {
            children.Insert(0, Ui.Reference($"{key}.ref", new UiChildRef(neuronRef.Kind, resolvedKey)));
        }

        return Ui.Card(
            key,
            neuronRef.NeuronUri,
            JsonSerializer.Serialize(new
            {
                _ui = "neuronRef",
                kind = neuronRef.Kind,
                neuronUri = neuronRef.NeuronUri,
                title = neuronRef.NeuronUri,
            }),
            [.. children]);
    }

    private static bool TryResolveBrainKey(string uriOrKey, string brainId, out string resolved)
    {
        resolved = string.Empty;
        if (string.IsNullOrWhiteSpace(uriOrKey)) { return false; }
        var trimmed = uriOrKey.Trim();
        if (trimmed.StartsWith("neuron://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed["neuron://".Length..];
        }
        if (trimmed.StartsWith(brainId + "/", StringComparison.Ordinal))
        {
            resolved = trimmed;
            return true;
        }
        if (!trimmed.Contains("://", StringComparison.Ordinal) && !trimmed.StartsWith('/'))
        {
            var slash = trimmed.IndexOf('/', StringComparison.Ordinal);
            if (slash < 0)
            {
                resolved = $"{brainId}/{trimmed}";
                return true;
            }
        }
        return false;
    }

    private static string FormatAction(ButtonAction action)
    {
        var op = string.IsNullOrWhiteSpace(action.Operation) ? "noop" : action.Operation;
        return action.Arguments.Count > 0
            ? op + ":" + string.Join(':', action.Arguments.Select(Uri.EscapeDataString))
            : op;
    }

    private static UiStyle MapButtonStyle(Button button)
    {
        var style = (button.Style ?? string.Empty).Trim().ToLowerInvariant();
        var tone = (button.Tone ?? string.Empty).Trim().ToLowerInvariant();
        if (tone == "danger" || style == "danger")
        {
            return UiStyle.Danger;
        }
        if (style == "primary" || tone == ButtonTone.Accent)
        {
            return UiStyle.Primary;
        }
        return style switch
        {
            "ghost" => UiStyle.Ghost,
            "link" => UiStyle.Link,
            _ => UiStyle.Secondary,
        };
    }

    private static UiTone MapToneString(string? tone) => (tone ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "done" or "ok" or "success" => UiTone.Done,
        "working" or "warn" or "warning" or "accent" or "info" => UiTone.Working,
        "error" or "danger" => UiTone.Error,
        _ => UiTone.Neutral,
    };

    private static string NonEmpty(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string SanitizeCopyText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return "neuron://"; }
        var chars = value.Where(c => c >= '\u0020' && (c < '\u007f' || c > '\u009f')).Take(2000).ToArray();
        return chars.Length == 0 ? "neuron://" : new string(chars);
    }

    private static string? SanitizeIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon)) { return null; }
        var normalized = new string(icon
            .Trim()
            .ToLowerInvariant()
            .Select(c => c == '_' || c == ' ' ? '-' : c)
            .Where(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')
            .Take(64)
            .ToArray())
            .Trim('-');
        return normalized.Length == 0 ? null : normalized;
    }
}
