using IntoChat.OS.Framework.Modules.Validation;
using MimeKit;

namespace IntoChat.OS.Modules.Google.Gmail;

internal static class GmailValidation
{
    internal const int MaximumRecipients = 100;
    internal const int MaximumSubjectBytes = 998;
    internal const int MaximumBodyBytes = 256 * 1024;
    internal const int MaximumPageTokenLength = 8192;

    public static GmailFailure? Validate(GmailInboxRequest? request) => Check(request, new InboxValidator());
    public static GmailFailure? Validate(GmailMessageRequest? request) => Check(request, new MessageValidator());
    public static GmailFailure? Validate(GmailDraftRequest? request) => Check(request, new DraftValidator());
    public static GmailFailure? Validate(GmailCreateDraftRequest? request) => Check(request, new CreateValidator());
    public static GmailFailure? Validate(GmailDraftOperationRequest? request) => Check(request, new OperationValidator());

    private static GmailFailure? Check<T>(T? request, IModuleValidator<T> validator) where T : class
    {
        var issues = request is null ? [new ValidationIssue("request", "required", "A request is required.")]
            : ModuleValidation.Validate(request, validator);
        return issues.Length == 0 ? null : new("invalid_input", "The Gmail request is invalid.",
            [.. issues.Select(issue => new GmailFieldIssue(issue.Path, issue.Code, issue.Message))]);
    }

    private static void Identifier(ValidationRules rules, List<ValidationIssue> issues, string path, string? value, int maximum)
    {
        rules.Required(path, value);
        rules.StringLength(path, value, 1, maximum);
        if (value?.Any(char.IsControl) == true) { issues.Add(new(path, "characters", "Control characters are not allowed.")); }
    }

    private sealed class InboxValidator : IModuleValidator<GmailInboxRequest>
    {
        public ValidationIssue[] Validate(GmailInboxRequest value)
        {
            var rules = new ValidationRules();
            List<ValidationIssue> issues = [];
            rules.Range("pageSize", value.PageSize, 1, 100);
            if (value.PageToken is not null) { Identifier(rules, issues, "pageToken", value.PageToken, MaximumPageTokenLength); }
            return [.. rules.Issues, .. issues];
        }
    }

    private sealed class MessageValidator : IModuleValidator<GmailMessageRequest>
    {
        public ValidationIssue[] Validate(GmailMessageRequest value)
        {
            var rules = new ValidationRules();
            List<ValidationIssue> issues = [];
            Identifier(rules, issues, "messageId", value.MessageId, 512);
            return [.. rules.Issues, .. issues];
        }
    }

    private sealed class DraftValidator : IModuleValidator<GmailDraftRequest>
    {
        public ValidationIssue[] Validate(GmailDraftRequest value)
        {
            var rules = new ValidationRules();
            List<ValidationIssue> issues = [];
            Recipients("to", value.To);
            Recipients("cc", value.Cc);
            Recipients("bcc", value.Bcc);
            rules.Range("recipients", (long)(value.To?.Length ?? 0) + (value.Cc?.Length ?? 0) + (value.Bcc?.Length ?? 0), 1, MaximumRecipients);
            Text("subject", value.Subject, MaximumSubjectBytes);
            Text("body", value.Body, MaximumBodyBytes);
            if (value.Subject?.Any(character => character is '\r' or '\n' or '\0') == true)
            { issues.Add(new("subject", "header", "Header line breaks and null characters are not allowed.")); }
            return [.. rules.Issues, .. issues];

            void Text(string path, string? text, int maximum)
            {
                if (text is null) { issues.Add(new(path, "required", "A string is required; it may be empty.")); }
                else { rules.Utf8Length(path, text, maximum); }
            }

            void Recipients(string path, string[]? addresses)
            {
                rules.Required(path, addresses);
                rules.ArrayLength(path, addresses, 0, MaximumRecipients);
                if (addresses is null) { return; }
                // An already-invalid oversized array must not cause unbounded address parsing work.
                for (var index = 0; index < Math.Min(addresses.Length, MaximumRecipients); index++)
                {
                    var address = addresses[index];
                    if (string.IsNullOrWhiteSpace(address) || address.Any(character => character is '\r' or '\n' or '\0')
                        || !MailboxAddress.TryParse(address, out var mailbox) || !mailbox.Address.Contains('@', StringComparison.Ordinal))
                    { issues.Add(new($"{path}[{index}]", "address", "Use one valid mailbox address without header line breaks.")); }
                }
            }
        }
    }

    private sealed class CreateValidator : IModuleValidator<GmailCreateDraftRequest>
    {
        public ValidationIssue[] Validate(GmailCreateDraftRequest value)
        {
            var rules = new ValidationRules();
            List<ValidationIssue> issues = [];
            Identifier(rules, issues, "previewId", value.PreviewId, 128);
            Identifier(rules, issues, "requestId", value.RequestId, 128);
            return [.. rules.Issues, .. issues];
        }
    }

    private sealed class OperationValidator : IModuleValidator<GmailDraftOperationRequest>
    {
        public ValidationIssue[] Validate(GmailDraftOperationRequest value)
        {
            var rules = new ValidationRules();
            List<ValidationIssue> issues = [];
            Identifier(rules, issues, "requestId", value.RequestId, 128);
            return [.. rules.Issues, .. issues];
        }
    }
}
