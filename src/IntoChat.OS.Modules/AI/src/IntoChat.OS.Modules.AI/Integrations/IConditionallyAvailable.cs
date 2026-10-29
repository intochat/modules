namespace IntoChat.OS.Modules.AI;

internal interface IConditionallyAvailable
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
