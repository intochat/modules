using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Serialization;

namespace IntoChat.OS.Modules.Google.Gmail.Tests.Unit;

public sealed class GmailContractFacts
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(typeof(GmailInboxResult))]
    [InlineData(typeof(GmailMessageResult))]
    [InlineData(typeof(GmailDraftPreviewResult))]
    [InlineData(typeof(GmailDraftCreationResult))]
    [InlineData(typeof(GmailDraftOperationResult))]
    public void ResultsHaveExactlyOneBranchAndRoundTripThroughJsonAndOrleans(Type type)
    {
        var constructor = Assert.Single(type.GetConstructors());
        var value = Value(type);
        var failure = new GmailFailure("rate_limited", "Try later.", [new("pageSize", "range", "Use at most 100 items.")], DateTimeOffset.UnixEpoch);
        Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() => constructor.Invoke([null, null])).InnerException);
        Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() => constructor.Invoke([value, failure])).InnerException);
        using var services = new ServiceCollection().AddSerializer().BuildServiceProvider();
        var serializer = services.GetRequiredService<Serializer>();
        foreach (var result in new[] { constructor.Invoke([value, null]), constructor.Invoke([null, failure]) })
        {
            var json = JsonSerializer.Serialize(result, type, Json);
            Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize(json, type, Json), type, Json));
            Assert.Equal(json, JsonSerializer.Serialize(serializer.Deserialize<object>(serializer.SerializeToArray(result)), type, Json));
        }
    }

    [Fact]
    public void ProviderCompletionDoesNotClaimAccountingHasSettled()
    {
        var operation = Operation();
        Assert.Equal(GmailDraftOperationStatus.Completed, operation.Status);
        Assert.Equal(GmailAccountingStatus.Pending, operation.Accounting!.Status);
        Assert.DoesNotContain("handle", JsonSerializer.Serialize(operation, Json), StringComparison.OrdinalIgnoreCase);
    }

    private static object Value(Type type) => type.Name switch
    {
        nameof(GmailInboxResult) => new GmailInboxPage([Summary()], "opaque-next", ["vanished"]),
        nameof(GmailMessageResult) => new GmailMessage(Summary(), ["to@example.test"], [], "", null, true,
            [new("1", "text/html", "attachment_data_not_fetched")], true),
        nameof(GmailDraftPreviewResult) => new GmailPreparedDraft("preview", ["to@example.test"], [], [], "", "body", DateTimeOffset.UnixEpoch.AddMinutes(10)),
        _ => Operation(),
    };

    private static GmailMessageSummary Summary() => new("message", "thread", "from@example.test", null, DateTimeOffset.UnixEpoch, "snippet", true);
    private static GmailDraftOperation Operation() => new("request", "preview", "mailbox@example.test", GmailDraftOperationStatus.Completed,
        "draft", null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1),
        new("trace", "invocation", GmailAccountingStatus.Pending));
}
