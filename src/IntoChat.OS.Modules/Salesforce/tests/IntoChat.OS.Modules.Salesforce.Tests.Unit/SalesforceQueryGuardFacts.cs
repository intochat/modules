namespace IntoChat.OS.Modules.Salesforce.Tests.Unit;

public sealed class SalesforceQueryGuardFacts
{
    [Theory]
    [InlineData("SELECT Id FROM Account WHERE Name = 'x' LIMIT 10")]
    [InlineData("select Id from Account a where a.Id != null limit 5 offset 10")]
    [InlineData("SELECT Id, (SELECT Id FROM Contacts) FROM Account WHERE Id != null LIMIT 1")]
    public void ABoundedSelectWithAnOuterWhereAndLimitIsAccepted(string query)
        => SalesforceQueryGuard.Validate(query);

    [Theory]
    [InlineData("SELECT Id FROM Account LIMIT 10")]
    [InlineData("SELECT Id FROM Account WHERE Id != null")]
    [InlineData("SELECT Id FROM Account WHERE Id != null LIMIT 0")]
    [InlineData("SELECT Id FROM Account WHERE Id != null LIMIT 5; DELETE")]
    [InlineData("SELECT Id FROM Account WHERE Id != null LIMIT 5 -- c")]
    [InlineData("SELECT Id FROM Account WHERE Id != null LIMIT 5 /* c */")]
    [InlineData("SELECT Id FROM Account WHERE Id != null LIMIT 5 FOR UPDATE")]
    [InlineData("SELECT Id FROM Account WHERE Id != null LIMIT 5 for view")]
    [InlineData("SELECT Id FROM Account WHERE Id != null LIMIT 5)")]
    [InlineData("SELECT Id FROM Account WHERE (Id != null LIMIT 5")]
    [InlineData("SELECT Id FROM Account WHERE Name = 'open LIMIT 5")]
    public void UnboundedLockingCommentedOrUnbalancedQueriesAreRejected(string query)
        => Assert.Throws<ArgumentException>(() => SalesforceQueryGuard.Validate(query));

    [Fact]
    public void AWhereOrLimitHiddenInsideALiteralOrSubqueryDoesNotCount()
    {
        Assert.Throws<ArgumentException>(() => SalesforceQueryGuard.Validate("SELECT Id FROM Account WHERE Id != null AND Name = 'x' LIMIT 'LIMIT 5'"));
        Assert.Throws<ArgumentException>(() => SalesforceQueryGuard.Validate("SELECT Id, (SELECT Id FROM Contacts WHERE Id != null LIMIT 1) FROM Account"));
    }

    [Fact]
    public void BlankAndOverlongQueriesAreRejected()
    {
        Assert.ThrowsAny<ArgumentException>(() => SalesforceQueryGuard.Validate(" "));
        Assert.Throws<ArgumentException>(() => SalesforceQueryGuard.Validate("SELECT Id FROM Account WHERE Id != null LIMIT 1 " + new string(' ', 20_001)));
    }
}
