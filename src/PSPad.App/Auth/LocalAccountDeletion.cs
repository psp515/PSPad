using Microsoft.Extensions.Logging;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;

namespace PSPad.App.Auth;

public sealed class LocalAccountDeletion(
    ILocalSessionStore sessions,
    IReplica replica,
    IOutbox outbox,
    LocalAuthenticationStateProvider authenticationState,
    ILogger<LocalAccountDeletion> logger)
{
    public async Task ClearAsync()
    {
        await ClearStorageAsync();
        Announce();
    }

    public void Announce() => authenticationState.SignedOut();

    public async Task ClearStorageAsync()
    {
        try
        {
            await replica.ClearAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning("Replica clear on account deletion failed: {Reason}", exception.Message);
        }

        try
        {
            await outbox.ClearAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning("Outbox clear on account deletion failed: {Reason}", exception.Message);
        }

        try
        {
            await sessions.ClearAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning("Session clear on account deletion failed: {Reason}", exception.Message);
        }
    }
}
