namespace IntoChat.OS.Modules.Google.Gmail.Tests.Unit;

public sealed class GmailValidationFacts
{
    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(100)]
    public void InboxAcceptsBoundedPages(int size) => Assert.Null(GmailValidation.Validate(new GmailInboxRequest(size)));

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void InboxRejectsOutOfRangePages(int size)
        => Assert.Contains(Assert.IsType<GmailFailure>(GmailValidation.Validate(new GmailInboxRequest(size))).Violations, issue => issue.Path == "pageSize");

    [Fact]
    public void NullRequestsBecomeStructuredFailures()
    {
        GmailFailure?[] failures = [GmailValidation.Validate((GmailInboxRequest?)null), GmailValidation.Validate((GmailMessageRequest?)null),
            GmailValidation.Validate((GmailDraftRequest?)null), GmailValidation.Validate((GmailCreateDraftRequest?)null), GmailValidation.Validate((GmailDraftOperationRequest?)null)];
        Assert.All(failures, failure => Assert.Equal("invalid_input", Assert.IsType<GmailFailure>(failure).Code));
    }

    [Fact]
    public void DraftLimitsCountEncodedBytesAndPermitEmptySubjectAndBody()
    {
        Assert.Null(GmailValidation.Validate(Draft("", "")));
        Assert.Null(GmailValidation.Validate(Draft(new string('é', 499), new string('é', 131072))));
        var failure = Assert.IsType<GmailFailure>(GmailValidation.Validate(Draft(new string('é', 499) + "a", new string('é', 131072) + "a")));
        Assert.Equal(["subject", "body"], failure.Violations.Select(issue => issue.Path));
    }

    [Fact]
    public void RecipientLimitsApplyAcrossAllArraysAndRejectNullArrays()
    {
        Assert.Null(GmailValidation.Validate(new GmailDraftRequest(Enumerable.Repeat("one@example.test", 99).ToArray(), ["two@example.test"], [], "", "")));
        Assert.Contains(Assert.IsType<GmailFailure>(GmailValidation.Validate(new GmailDraftRequest(Enumerable.Repeat("one@example.test", 100).ToArray(), ["two@example.test"], [], "", ""))).Violations,
            issue => issue.Path == "recipients");
        Assert.Contains(Assert.IsType<GmailFailure>(GmailValidation.Validate(new GmailDraftRequest([], [], [], "", ""))).Violations, issue => issue.Path == "recipients");
        Assert.Contains(Assert.IsType<GmailFailure>(GmailValidation.Validate(new GmailDraftRequest(null!, [], [], "", ""))).Violations, issue => issue.Path == "to");
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("secret-sentinel@example.test\r\nBcc: attacker@example.test")]
    [InlineData("one@example.test, two@example.test")]
    public void InvalidRecipientHeadersProduceSafePreciseIssues(string recipient)
    {
        var failure = Assert.IsType<GmailFailure>(GmailValidation.Validate(new GmailDraftRequest([recipient], [], [], "", "")));
        Assert.Contains(failure.Violations, issue => issue.Path == "to[0]");
        Assert.DoesNotContain("secret-sentinel", failure.Message + string.Join(' ', failure.Violations.Select(issue => issue.Message)), StringComparison.Ordinal);
    }

    [Fact]
    public void SubjectInjectionAndNullTextAreInvalidButOpaqueIdsAreNotAssumedToBeHex()
    {
        Assert.Contains(Assert.IsType<GmailFailure>(GmailValidation.Validate(Draft("hello\nBcc: other", ""))).Violations, issue => issue.Path == "subject");
        Assert.Equal(2, Assert.IsType<GmailFailure>(GmailValidation.Validate(Draft(null!, null!))).Violations.Length);
        Assert.Null(GmailValidation.Validate(new GmailMessageRequest("opaque/id+value=")));
        Assert.NotNull(GmailValidation.Validate(new GmailCreateDraftRequest("", "request")));
        Assert.NotNull(GmailValidation.Validate(new GmailDraftOperationRequest(new string('x', 129))));
    }

    private static GmailDraftRequest Draft(string subject, string body) => new(["recipient@example.test"], [], [], subject, body);
}
