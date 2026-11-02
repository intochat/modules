namespace IntoChat.OS.Modules.Microsoft.GitHub;

[GenerateSerializer, Alias("github.reply-review-comment")]
public sealed record ReplyReviewComment(
    [property: Id(0)] int PullRequestNumber,
    [property: Id(1)] long CommentId,
    [property: Id(2)] string Body);
