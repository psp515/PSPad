using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;

namespace PSPad.App.Auth;

public sealed class LocalAccountDeletion(
    ILocalSessionStore sessions,
    IReplica replica,
    IOutbox outbox,
    LocalAuthenticationStateProvider authenticationState)
{
    public async Task ClearAsync()
    {
        await replica.ClearAsync();
        await outbox.ClearAsync();
        await sessions.ClearAsync();
        authenticationState.SignedOut();
    }
}
