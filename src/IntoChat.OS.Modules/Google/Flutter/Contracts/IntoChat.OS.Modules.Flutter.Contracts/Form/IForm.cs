using DigitalBrain.Contracts;
using DigitalBrain;
using IntoChat.OS;
using DigitalBrain.State;

namespace IntoChat.OS.Modules.Flutter.Form;

// One declarative form per window: a typed field list from the P1.1 catalog, draft values, and a
// single atomic submit. There is no per-field neuron; the whole form is one grain and one blob.
[NeuronAlias("form"), DefaultNeuronType(UIVocabulary.FormType)]
public interface IForm
{
    Task<FormState> Define(FormDefinition definition);
    Task<FormState> SetDraft(string fieldName, string value);
    Task<FormState> SetSecret(string fieldName, FormSecret reference);
    Task<FormState> Submit(FormSubmission submission);
    [DigitalBrain.ReadOnlyCall] Task<FormState> Read();
}

public enum FormFieldKind { PlainText, LongText, Number, Date, DateTime, Boolean, Choice, MultiChoice, Email, Url, Secret, Reference }

// A secret the form holds by reference only; the value lives in the platform ring.
public sealed record FormSecret(string Reference, string Label, bool IsSet);

public sealed class FormValidationException(FormFieldKind kind, string? rejectedValue, string[] allowedValues)
    : ArgumentException($"{(kind == FormFieldKind.Secret ? "(redacted)" : $"'{rejectedValue}'")} is not a valid {kind} value. Allowed {kind} values: {string.Join(", ", allowedValues)}.")
{
    public FormFieldKind Kind { get; } = kind;
    public string? RejectedValue { get; } = kind == FormFieldKind.Secret ? null : rejectedValue;
    public string[] AllowedValues { get; } = allowedValues;
}

public sealed record FormField(
    string Name,
    string Label,
    FormFieldKind Kind,
    bool Required = false,
    IReadOnlyList<string>? Choices = null);

public sealed record FormDefinition(
    string Title,
    IReadOnlyList<FormField> Fields);

public sealed record FormFieldValue(
    string Name,
    string? Value = null,
    FormSecret? Secret = null);

public sealed record FormSubmission(
    IReadOnlyList<FormFieldValue> Values,
    int ExpectedRevision = 0);

public sealed class FormFieldState
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public FormFieldKind Kind { get; set; }
    public bool Required { get; set; }
    public List<string> Choices { get; set; } = [];
    public string? Value { get; set; }
    public bool SecretSet { get; set; }
    public bool Supported { get; set; }
    public FormSecret? Secret { get; set; }
}

[ExportableState]
public sealed class FormState
{
    public string FormId { get; set; } = "";
    public string Title { get; set; } = "";
    public int Revision { get; set; }
    public List<FormFieldState> Fields { get; set; } = [];
    public bool Submitted { get; set; }
}

// Kinds without a dedicated input render as plain text; the fallback is declared, never a compile
// or deploy. The catalog still tells the assistant the real kind and sensitivity.
public static class FormFieldSupport
{
    public const string FallbackExplanation = "This field kind has no dedicated input yet and is shown as plain text.";

    public static bool Supports(FormFieldKind kind) => kind != FormFieldKind.Reference;
}
