using DigitalBrain;
using System.Globalization;
using IntoChat.OS.Modules.Flutter.Form.Signals;
using IntoChat.OS.Connections;
using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;

namespace IntoChat.OS.Modules.Flutter.Form;

[Neuron(UIVocabulary.FormType)]
internal sealed class FormNeuron
    : UiNeuron<FormState>, IForm
{
    public Task<FormState> Define(FormDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Title);
        if (definition.Title.Length > 200) { throw new ArgumentException("A form title is limited to 200 characters.", nameof(definition)); }
        if (definition.Fields.Count is 0 or > 64) { throw new ArgumentException("A form declares 1–64 fields.", nameof(definition)); }
        var names = new HashSet<string>(StringComparer.Ordinal);
        var fields = new List<FormFieldState>(definition.Fields.Count);
        foreach (var field in definition.Fields)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(field.Name);
            ArgumentException.ThrowIfNullOrWhiteSpace(field.Label);
            if (field.Name.Length > 64) { throw new ArgumentException("A field name is limited to 64 characters.", nameof(definition)); }
            if (field.Label.Length > 120) { throw new ArgumentException("A field label is limited to 120 characters.", nameof(definition)); }
            if (!names.Add(field.Name)) { throw new ArgumentException($"Field '{field.Name}' is declared twice.", nameof(definition)); }
            fields.Add(new FormFieldState
            {
                Name = field.Name,
                Label = field.Label,
                Kind = field.Kind,
                Required = field.Required,
                Choices = [.. field.Choices ?? []],
                Supported = FormFieldSupport.Supports(field.Kind),
            });
        }
        var state = Snapshot;
        state.FormId = FullKey;
        state.Title = definition.Title.Trim();
        state.Fields = fields;
        state.Submitted = false;
        state.Revision++;
        return SaveAndReturn();
    }

    public Task<FormState> SetDraft(string fieldName, string value)
    {
        var field = Field(fieldName);
        ArgumentNullException.ThrowIfNull(value);
        if (field.Kind == FormFieldKind.Secret) { throw new ArgumentException("A secret field only accepts a FormSecret; raw secret values are never accepted.", nameof(value)); }
        field.Value = value;
        Snapshot.Revision++;
        return SaveAndReturn();
    }

    public Task<FormState> SetSecret(string fieldName, FormSecret reference)
    {
        var field = Field(fieldName);
        if (field.Kind != FormFieldKind.Secret) { throw new ArgumentException($"Field '{fieldName}' is not a secret field.", nameof(fieldName)); }
        Validate(field.Kind, ToRef(reference));
        field.SecretSet = true;
        field.Secret = reference;
        field.Value = null;
        Snapshot.Revision++;
        return SaveAndReturn();
    }

    // All values are validated before any field is touched, then written in one state write.
    public Task<FormState> Submit(FormSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (submission.ExpectedRevision != 0 && submission.ExpectedRevision != Snapshot.Revision)
        { throw new RevisionConflictException($"Expected form revision {submission.ExpectedRevision}; current revision is {Snapshot.Revision}.", Snapshot.Revision); }
        var provided = new Dictionary<string, FormFieldValue>(StringComparer.Ordinal);
        foreach (var value in submission.Values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Name);
            if (!provided.TryAdd(value.Name, value)) { throw new ArgumentException($"Field '{value.Name}' is submitted twice.", nameof(submission)); }
        }
        foreach (var name in provided.Keys)
        {
            if (!Snapshot.Fields.Any(field => field.Name == name)) { throw new ArgumentException($"The form has no field '{name}'.", nameof(submission)); }
        }
        var normalized = new List<(FormFieldState Field, string? Value, bool SecretSet, FormSecret? Secret)>(Snapshot.Fields.Count);
        foreach (var field in Snapshot.Fields)
        {
            provided.TryGetValue(field.Name, out var submitted);
            if (field.Kind == FormFieldKind.Secret)
            {
                if (submitted?.Secret is null)
                {
                    if (submitted?.Value is not null) { throw Invalid(field.Kind, submitted.Value, TypeCatalog.AllowedFor(Catalog(field.Kind))); }
                    if (field.Required && !field.SecretSet) { throw Invalid(field.Kind, null, TypeCatalog.AllowedFor(Catalog(field.Kind))); }
                    continue;
                }
                Validate(field.Kind, ToRef(submitted.Secret));
                normalized.Add((field, null, true, submitted.Secret));
                continue;
            }
            if (string.IsNullOrEmpty(submitted?.Value))
            {
                if (field.Required) { throw Invalid(field.Kind, submitted?.Value, TypeCatalog.AllowedFor(Catalog(field.Kind))); }
                continue;
            }
            var text = submitted!.Value!;
            if (field.Kind == FormFieldKind.Choice && field.Choices.Count > 0 && !field.Choices.Contains(text, StringComparer.Ordinal))
            { throw Invalid(field.Kind, text, field.Choices); }
            if (field.Kind == FormFieldKind.MultiChoice && field.Choices.Count > 0)
            {
                foreach (var choice in text.Split('\n'))
                {
                    if (!field.Choices.Contains(choice, StringComparer.Ordinal)) { throw Invalid(field.Kind, choice, field.Choices); }
                }
            }
            var input = field.Kind == FormFieldKind.MultiChoice ? text.Split('\n') : (object)text;
            normalized.Add((field, Normalize(field.Kind, Validate(field.Kind, input)), false, null));
        }
        foreach (var (field, value, secretSet, secret) in normalized)
        {
            field.Value = value;
            field.SecretSet = secretSet;
            if (secret is not null) { field.Secret = secret; }
        }
        Snapshot.Submitted = true;
        Snapshot.Revision++;
        return SaveAndReturn();
    }

    [DigitalBrain.ReadOnlyCall]
    public Task<FormState> Read()
    {
        Snapshot.FormId = FullKey;
        return Task.FromResult(CallerContextStamper.TryGet(out var caller) && CallerContextStamper.IsModelOriginated(caller) ? Shape(Snapshot) : Snapshot);
    }

    // What a model may see of a form: its fields, status and revision, never a value or a secret reference.
    private static FormState Shape(FormState state) => new()
    {
        FormId = state.FormId,
        Title = state.Title,
        Revision = state.Revision,
        Submitted = state.Submitted,
        Fields = [.. state.Fields.Select(field => new FormFieldState
        {
            Name = field.Name,
            Label = field.Label,
            Kind = field.Kind,
            Required = field.Required,
            Choices = [.. field.Choices],
            SecretSet = field.SecretSet,
            Supported = field.Supported,
        })],
    };

    private FormFieldState Field(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Snapshot.Fields.SingleOrDefault(field => field.Name == name)
            ?? throw new KeyNotFoundException($"The form has no field '{name}'.");
    }

    private async Task<FormState> SaveAndReturn()
    {
        var state = Snapshot;
        await Save(state, new FormChanged(FullKey, state.Revision));
        return state;
    }

    private static object Validate(FormFieldKind kind, object input)
    {
        try { return TypeCatalog.Validate(input, Catalog(kind)); }
        catch (TypeValidationException error) { throw Invalid(kind, error.RejectedValue, error.AllowedValues); }
    }

    private static FieldKind Catalog(FormFieldKind kind) => (FieldKind)(int)kind;
    private static SecretRef ToRef(FormSecret secret) => new() { Reference = secret.Reference, Label = secret.Label, Status = secret.IsSet ? SecretStatus.Set : SecretStatus.Unset };
    private static FormValidationException Invalid(FormFieldKind kind, object? value, IReadOnlyList<string> allowed) => new(kind, value?.ToString(), [.. allowed]);

    private static string Normalize(FormFieldKind kind, object validated) => kind switch
    {
        FormFieldKind.Date => ((DateOnly)validated).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        FormFieldKind.DateTime => ((DateTime)validated).ToString("O", CultureInfo.InvariantCulture),
        FormFieldKind.Number => ((decimal)validated).ToString(CultureInfo.InvariantCulture),
        FormFieldKind.Boolean => (bool)validated ? "true" : "false",
        FormFieldKind.MultiChoice => string.Join('\n', (string[])validated),
        _ => (string)validated,
    };
}
