using IntoChat.OS.Runtime;
using DigitalBrain.Spi;
using System.Text.Json;
using IntoChat.OS.Confirmation;
using IntoChat.OS.Modules.Apps;
using IntoChat.OS.Modules.Flutter.Button;
using IntoChat.OS.Modules.Flutter.Card;
using IntoChat.OS.Modules.Flutter.Chart;
using IntoChat.OS.Modules.Flutter.Chat;
using IntoChat.OS.Modules.Flutter.Collection;
using IntoChat.OS.Modules.Flutter.Expander;
using IntoChat.OS.Modules.Flutter.FileInput;
using IntoChat.OS.Modules.Flutter.Image;
using IntoChat.OS.Modules.Flutter.Form;
using IntoChat.OS.Modules.Flutter.ImageCanvas;
using IntoChat.OS.Modules.Flutter.InfoBar;
using IntoChat.OS.Modules.Flutter.Layout;
using IntoChat.OS.Modules.Flutter.Progress;
using IntoChat.OS.Modules.Flutter.Select;
using IntoChat.OS.Modules.Flutter.Space;
using IntoChat.OS.Modules.Flutter.Surface;
using IntoChat.OS.Modules.Flutter.Table;
using IntoChat.OS.Modules.Flutter.Tabs;
using IntoChat.OS.Modules.Flutter.Text;
using IntoChat.OS.Modules.Flutter.TextField;
using IntoChat.OS.Modules.Flutter.Toggle;
using IntoChat.OS.Modules.Flutter.Entries;
using IntoChat.OS.Modules.Flutter.Preferences;
using IntoChat.OS.Modules.Flutter.Viewport;
using IntoChat.OS.Modules.Flutter.Views;
using IntoChat.OS.Modules.Flutter.VoiceInput;
using IntoChat.OS.Modules.Flutter.WebBrowser;
using IntoChat.OS.Modules.Flutter.Workspace;
using IntoChat.OS.Connections;
using IntoChat.OS.Framework;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using DigitalBrain.Runtime.Orleans.Hosting;
using MicrosoftSdk = Microsoft;

namespace IntoChat.OS.Modules.Flutter;

internal static class AppUiEndpoints
{
    // GET /apps/node?kind=view writes the tree with these options; the other kinds use the host's HTTP JSON options.
    internal static readonly JsonSerializerOptions ViewJson = new(JsonSerializerDefaults.Web) { MaxDepth = 128 };

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var apps = BrainRoutes.Group(endpoints, "/apps").AddEndpointFilter(ModuleRouteGuard.Guard);
        apps.MapPost("/connect", async ([FromServices] IEnumerable<IUiRootProvider> providers, CancellationToken ct) =>
        {
            var provider = providers.SingleOrDefault();
            return Results.Ok(new { Root = provider is null ? null : await provider.Connect(ct), provider?.ActivationApp });
        });
        apps.MapGet("/node", async (string kind, string name, INeuronProxies brain, CancellationToken ct) =>
        {
            var scope = BrainScope.CurrentId();
            if (kind == UIVocabulary.ConfirmationType)
            {
                if (name != IConfirmationInbox.Key(scope)) { throw new UnauthorizedAccessException(); }
                return Results.Json(ConfirmationView.Render(await brain.Get<IConfirmationInbox>(name).Read().WaitAsync(ct)), ViewJson);
            }
            var space = brain.Get<ISpace>(ISpace.Key(scope));
            if (kind == "person" && name.EndsWith("/profile", StringComparison.Ordinal))
            { return Results.Ok(await space.ReadProfile(name).WaitAsync(ct)); }
            if (kind == "card" && CircleMembers.PostBrain(name) is not null)
            { return Results.Ok(await space.ReadPost(name).WaitAsync(ct)); }
            if (kind == "collection" && name.EndsWith("/posts", StringComparison.Ordinal))
            { return Results.Ok(await space.ReadPosts(name).WaitAsync(ct)); }
            if (!OwnsUi(scope, name)) { throw new UnauthorizedAccessException("The UI belongs to another brain."); }
            if (kind == UIVocabulary.ViewType) { name = await ResolveViewAlias(brain, scope, name, ct); }
            if (kind == "viewport")
            {
                var viewport = await brain.Get<IViewport>(name).Read().WaitAsync(ct);
                if (viewport.Target is not { } target)
                { return Results.Ok(new { viewport.Revision, Root = (UiChildRef?)null, Error = (string?)null }); }
                if (!OwnsUi(scope, target.App)) { throw new UnauthorizedAccessException(); }
                var app = await brain.Get<IApp>(target.App).Read().WaitAsync(ct);
                if (app.MigrationTarget is { } migrated)
                {
                    if (!OwnsUi(scope, migrated)) { throw new UnauthorizedAccessException(); }
                    app = await brain.Get<IApp>(migrated).Read().WaitAsync(ct);
                }
                var presentation = app.Presentation;
                var root = presentation?.Root?.Views?.FirstOrDefault(view => view.Id == target.Variant)?.Root;
                if (root is not null && !OwnsUi(scope, root.Name)) { throw new UnauthorizedAccessException(); }
                return Results.Ok(new
                {
                    viewport.Revision,
                    viewport.Target,
                    Root = root,
                    Error = presentation?.Error ?? (root is null ? "This app has not published this view." : null)
                });
            }
            return kind switch
            {
                UIVocabulary.ViewType => Results.Json(await brain.Get<IView>(name).Read().WaitAsync(ct), ViewJson),
                "text" => Results.Ok(await brain.Get<IText>(name).Read().WaitAsync(ct)),
                "button" => Results.Ok(await brain.Get<IButton>(name).Read().WaitAsync(ct)),
                "textfield" => Results.Ok(await brain.Get<ITextField>(name).Read().WaitAsync(ct)),
                "select" => Results.Ok(await brain.Get<ISelect>(name).Read().WaitAsync(ct)),
                "fileinput" => Results.Ok(await brain.Get<IFileInput>(name).Read().WaitAsync(ct)),
                "card" => Results.Ok(await brain.Get<ICard>(name).Read().WaitAsync(ct)),
                "tabs" => Results.Ok(await brain.Get<ITabs>(name).Read().WaitAsync(ct)),
                "surface" => Results.Ok(await brain.Get<ISurface>(name).Read().WaitAsync(ct)),
                "layout" => Results.Ok(await brain.Get<ILayout>(name).Read().WaitAsync(ct)),
                "collection" => Results.Ok(await brain.Get<ICollectionView>(name).Read().WaitAsync(ct)),
                "imagecanvas" => Results.Ok(await brain.Get<IImageCanvas>(name).Read().WaitAsync(ct)),
                "form" => Results.Ok(await brain.Get<IForm>(name).Read().WaitAsync(ct)),
                "table" => Results.Ok(await brain.Get<ITable>(name).Read().WaitAsync(ct)),
                "chart" => Results.Ok(await brain.Get<IChart>(name).Read().WaitAsync(ct)),
                UIVocabulary.ToggleType => Results.Ok(await brain.Get<IToggle>(name).Read().WaitAsync(ct)),
                UIVocabulary.EntryControlType => Results.Ok(await brain.Get<IEntryControl>(name).Read().WaitAsync(ct)),
                UIVocabulary.PreferenceType => Results.Ok(await brain.Get<IPreferenceControl>(name).Read().WaitAsync(ct)),
                UIVocabulary.ProgressType => Results.Ok(await brain.Get<IProgress>(name).Read().WaitAsync(ct)),
                UIVocabulary.InfoBarType => Results.Ok(await brain.Get<IInfoBar>(name).Read().WaitAsync(ct)),
                UIVocabulary.ImageType => Results.Ok(await brain.Get<IImage>(name).Read().WaitAsync(ct)),
                UIVocabulary.ExpanderType => Results.Ok(await brain.Get<IExpander>(name).Read().WaitAsync(ct)),
                UIVocabulary.ChatType => Results.Ok(await brain.Get<IChat>(name).Read().WaitAsync(ct)),
                UIVocabulary.ComposerType => Results.Ok(await brain.Get<IComposer>(name).Read().WaitAsync(ct)),
                UIVocabulary.VoiceInputType => Results.Ok(await brain.Get<IVoiceInput>(name).Read().WaitAsync(ct)),
                UIVocabulary.WebBrowserType => Results.Ok(await brain.Get<IWebBrowser>(name).Read().WaitAsync(ct)),
                _ => throw new ArgumentException("Unknown app UI kind.")
            };
        });
        apps.MapPost("/event", async (UiEvent input, INeuronProxies brain, CancellationToken ct) =>
        {
            var scope = BrainScope.CurrentId();
            if (input.Kind == UIVocabulary.ConfirmationType)
            {
                if (input.Name != IConfirmationInbox.Key(scope)) { throw new UnauthorizedAccessException(); }
                var answer = ConfirmationView.Answer(input.Action);
                await brain.Get<IConfirmationInbox>(input.Name).Resolve(answer.PlanId, answer.Approve).WaitAsync(ct);
                return Results.Ok(new { delivered = true });
            }
            if (input.Kind == "collection" && input.Name.EndsWith("/posts", StringComparison.Ordinal))
            {
                var posts = await brain.Get<ISpace>(ISpace.Key(scope)).ReadPosts(input.Name).WaitAsync(ct);
                if (input.Action != "activate" || !posts.Definition.Items.Any(item => item.Id == input.Value))
                { throw new UnauthorizedAccessException("Only a visible post can be opened."); }
                var workspace = brain.Get<IWorkspace>(IWorkspace.Key(scope));
                await workspace.Show(input.Value!, input.OperationId?.ToString() ?? Guid.NewGuid().ToString(), (await workspace.Read()).Revision).WaitAsync(ct);
                return Results.Accepted();
            }
            if (!OwnsUi(scope, input.Name)) { throw new UnauthorizedAccessException(); }
            if (input.Kind == UIVocabulary.ViewType)
            { input = input with { Name = await ResolveViewAlias(brain, scope, input.Name, ct) }; }
            switch (input.Kind)
            {
                case UIVocabulary.ViewType:
                    await brain.Get<IView>(input.Name).Activate(input.Revision, input.Field ?? "", input.Action ?? "", input.OperationId ?? Guid.Empty).WaitAsync(ct);
                    break;
                case UIVocabulary.ChatType:
                    var chat = brain.Get<IChat>(input.Name);
                    if (input.Read is { } read)
                    {
                        if (input.Action is not null) { throw new ArgumentException("Choose a chat action or a read acknowledgement."); }
                        await chat.MarkRead(input.Context ?? "", input.Revision, read.Version, read.MessageIds).WaitAsync(ct);
                    }
                    else { await chat.Activate(input.Context ?? "", input.Revision, input.Action ?? "").WaitAsync(ct); }
                    break;
                case UIVocabulary.ComposerType:
                    var composer = brain.Get<IComposer>(input.Name);
                    switch (input.Action)
                    {
                        case "edit": await (input.SelectionRevision is { } editGeneration ? composer.Edit(input.Context ?? "", editGeneration, input.Value ?? "") : composer.Edit(input.Context ?? "", input.Value ?? "")).WaitAsync(ct); break;
                        case "submit": await (input.SelectionRevision is { } submitGeneration ? composer.Submit(input.OperationId ?? Guid.Empty, input.Context ?? "", submitGeneration, input.Value ?? "") : composer.Submit(input.OperationId ?? Guid.Empty, input.Context ?? "", input.Value ?? "")).WaitAsync(ct); break;
                        case "voice-begin": await (input.SelectionRevision is { } voiceGeneration ? composer.BeginVoice(input.OperationId ?? Guid.Empty, input.Context ?? "", voiceGeneration) : composer.BeginVoice(input.OperationId ?? Guid.Empty, input.Context ?? "")).WaitAsync(ct); break;
                        case "voice-audio": await composer.CaptureVoice(input.OperationId ?? Guid.Empty, DecodeAudio(input.Value), input.MimeType ?? "").WaitAsync(ct); break;
                        case "voice-cancel": await composer.CancelVoice(input.OperationId ?? Guid.Empty).WaitAsync(ct); break;
                        case "voice-error": await composer.CompleteVoice(input.OperationId ?? Guid.Empty, null, input.Value ?? "Recording failed. Try again.").WaitAsync(ct); break;
                        default: throw new ArgumentException("Unknown composer action.");
                    }
                    break;
                case UIVocabulary.WebBrowserType:
                    var browser = brain.Get<IWebBrowserConnector>(input.Name);
                    if (input.Action == "unavailable")
                    {
                        await browser.Unavailable(input.Value ?? "The browser is unavailable.").WaitAsync(ct);
                        break;
                    }
                    BrowserConnect connection;
                    try { connection = JsonSerializer.Deserialize<BrowserConnect>(input.Value ?? "", JsonSerializerOptions.Web) ?? throw new ArgumentException("Browser connection is required."); }
                    catch (JsonException) { throw new ArgumentException("Invalid browser connection."); }
                    if (input.Action == "connect") { await browser.Connect(connection.Port, connection.SessionId).WaitAsync(ct); }
                    else if (input.Action == "disconnect") { await browser.Disconnect(connection.SessionId).WaitAsync(ct); }
                    else { throw new ArgumentException("Unknown browser action."); }
                    break;
                case "button": await brain.Get<IButton>(input.Name).ClickCurrent(input.Action ?? "").WaitAsync(ct); break;
                case "textfield": await brain.Get<ITextField>(input.Name).Input(input.Value ?? "").WaitAsync(ct); break;
                case "select": await brain.Get<ISelect>(input.Name).Choose(input.Value ?? "").WaitAsync(ct); break;
                case "fileinput": await brain.Get<IFileInput>(input.Name).Capture(input.Field ?? "", input.Value ?? "").WaitAsync(ct); break;
                case "tabs": await brain.Get<ITabs>(input.Name).Select(input.Value ?? "").WaitAsync(ct); break;
                case UIVocabulary.ToggleType when input.Action == "flip": await brain.Get<IToggle>(input.Name).Flip().WaitAsync(ct); break;
                case UIVocabulary.EntryControlType when input.Action == "changed": await brain.Get<IEntryControl>(input.Name).Changed().WaitAsync(ct); break;
                case UIVocabulary.PreferenceType when input.Action == "set": await brain.Get<IPreferenceControl>(input.Name).Set(input.Value ?? "").WaitAsync(ct); break;
                case UIVocabulary.InfoBarType when input.Action == "dismiss": await brain.Get<IInfoBar>(input.Name).Dismiss().WaitAsync(ct); break;
                case UIVocabulary.ExpanderType when input.Action == "toggle": await brain.Get<IExpander>(input.Name).Toggle().WaitAsync(ct); break;
                case "collection":
                    var collection = brain.Get<ICollectionView>(input.Name);
                    if (input.Action == "select") { await collection.Select(input.Value ?? "", input.Revision).WaitAsync(ct); }
                    else { await collection.Activate(input.Value ?? "", input.Revision).WaitAsync(ct); }
                    break;
                case "form":
                    var form = brain.Get<IForm>(input.Name);
                    if (input.Action == "submit")
                    {
                        var state = await form.Read().WaitAsync(ct);
                        var values = state.Fields.Select(field => new FormFieldValue(field.Name, field.Value)).ToArray();
                        await form.Submit(new(values, (int)input.Revision)).WaitAsync(ct);
                    }
                    else if (input.Action == "secret")
                    {
                        if (!SecretReferences.IsReference(input.Value))
                        {
                            throw new ArgumentException("A form secret carries the vault reference, never a raw secret value.");
                        }

                        await form.SetSecret(input.Field ?? "", new FormSecret(input.Value!, input.Field ?? "", true)).WaitAsync(ct);
                    }
                    else { await form.SetDraft(input.Field ?? "", input.Value ?? "").WaitAsync(ct); }
                    break;
                case UIVocabulary.VoiceInputType:
                    await brain.Get<IVoiceInput>(input.Name).Capture(DecodeAudio(input.Value), input.MimeType ?? "").WaitAsync(ct);
                    break;
                default: throw new ArgumentException("Unknown UI event.");
            }
            return Results.Ok(new { delivered = true });
        });

        UiHttp.MapGet<ITable, TableState>(endpoints, "tables", neuron => neuron.Read());
        UiHttp.MapPost(endpoints, "tables/{name}/replace", async (string name, TableReplace body, [MicrosoftSdk.AspNetCore.Mvc.FromServices] IServiceProvider plain, CancellationToken ct) =>
        {
            await SiloNeurons.Get<ITable>(plain, name).Replace(body.Title, body.Columns, body.Rows).WaitAsync(ct);
            return Results.Accepted();
        });
    }

    private static bool OwnsUi(string scope, string name) =>
        new[] { "/apps/", "/images/", "/applications/" }.Any(area => name.StartsWith(scope + area, StringComparison.Ordinal));

    private static byte[] DecodeAudio(string? base64)
    {
        try { return Convert.FromBase64String(base64 ?? ""); }
        catch (FormatException) { throw new ArgumentException("Audio must be base64 encoded."); }
    }

    private static async Task<string> ResolveViewAlias(INeuronProxies brain, string scope, string name, CancellationToken ct)
    {
        if (!name.EndsWith("/view", StringComparison.Ordinal)) { return name; }
        var appKey = name[..^5];
        if (IApp.NameOf(scope, appKey) is null) { return name; }
        var app = await brain.Get<IApp>(appKey).Read().WaitAsync(ct);
        if (app.MigrationTarget is not { } target) { return name; }
        if (!OwnsUi(scope, target)) { throw new UnauthorizedAccessException(); }
        return target + "/view";
    }

    internal sealed record UiEvent(string Kind, string Name, string? Action = null, string? Value = null, long Revision = 0, string? Field = null, string? MimeType = null,
        string? Context = null, Guid? OperationId = null, ChatReadInput? Read = null, long? SelectionRevision = null);
    internal sealed record ChatReadInput(long Version, string[] MessageIds);
    private sealed record BrowserConnect(int Port, string SessionId);
}
