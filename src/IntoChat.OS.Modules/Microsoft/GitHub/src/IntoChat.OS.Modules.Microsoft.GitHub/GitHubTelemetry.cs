using System.Diagnostics;

namespace IntoChat.OS.Modules.Microsoft.GitHub;

internal static class GitHubTelemetry
{
    internal static readonly ActivitySource Source = new("IntoChat.OS.GitHub");

    internal static void TagReceipt(Activity? activity, GitHubRepository repository, string deliveryId, int? number)
    {
        activity?.SetTag("github.installation.id", repository.InstallationId)
            .SetTag("github.repository.id", repository.RepositoryId)
            .SetTag("github.delivery.id", deliveryId);
        if (number is { } value)
        {
            activity?.SetTag("github.pull_request.number", value);
        }
    }

    internal static void Failed(Activity? activity, Exception error)
        => activity?.SetTag("error.type", error.GetType().Name).SetStatus(ActivityStatusCode.Error, error.GetType().Name);
}
