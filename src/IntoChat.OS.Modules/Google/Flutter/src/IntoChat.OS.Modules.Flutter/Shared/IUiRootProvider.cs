namespace IntoChat.OS.Modules.Flutter;

// A distribution selects its app-authored root and navigation receiver.
public interface IUiRootProvider
{
    string? ActivationApp => null;
    Task<UiChildRef> Connect(CancellationToken cancellationToken);
}
