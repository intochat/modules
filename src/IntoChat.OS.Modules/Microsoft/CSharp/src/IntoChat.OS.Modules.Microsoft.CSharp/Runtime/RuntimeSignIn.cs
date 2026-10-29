using IntoChat.OS.Contracts.Configuration;
using IntoChat.OS.Framework;
using IntoChat.OS.Connections.Platform.Identity;
using IntoChat.OS.Framework.Modules.Platform.Identity;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Runtime;

// Sign-in warms the brain's sandbox session, so behaviors are alive before the first window opens.
internal sealed class RuntimeSignIn(IIntoChatOSBrains brains) : ISignInHandler
{
    public Task SignedIn(Member member, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);
        var brainId = BrainScope.Create(member.AccountId, member.BrainId).Id;
        return brains.Get<IBrainRuntime>(BrainRuntimeKeys.For(brainId)).Touch(cancellationToken);
    }
}
