using Microsoft.Extensions.Logging;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;

namespace PSPad.App.Auth;

public sealed class LocalSignOut(
    ILocalSessionStore sessions,
    IReplica replica,
    IOutbox outbox,
    KeycloakTokenRevoker revoker,
    LocalAuthenticationStateProvider authenticationState,
    ILogger<LocalSignOut> logger)
{
    public async Task<SignOutOutcome> SignOutAsync()
    {
        LocalSession? session;

        try
        {
            session = await sessions.LoadAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning("Session load on sign-out failed: {Reason}", exception.Message);
            session = null;
        }

        if (session is not null)
        {
            var revocation = await revoker.RevokeAsync(session.RefreshToken);

            if (revocation is RevocationOutcome.Unreachable)
            {
                return new SignOutOutcome.KeycloakUnreachable();
            }

            if (revocation is RevocationOutcome.Refused)
            {
                logger.LogWarning("Keycloak refused to revoke the refresh token on sign-out; ending the session anyway");
            }
        }

        await ClearStorageAsync();
        return new SignOutOutcome.Cleared(session?.IdToken);
    }

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
            logger.LogWarning("Replica clear on sign-out failed: {Reason}", exception.Message);
        }

        try
        {
            await outbox.ClearAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning("Outbox clear on sign-out failed: {Reason}", exception.Message);
        }

        try
        {
            await sessions.ClearAsync();
        }
        catch (Exception exception)
        {
            logger.LogWarning("Session clear on sign-out failed: {Reason}", exception.Message);
        }
    }
}
