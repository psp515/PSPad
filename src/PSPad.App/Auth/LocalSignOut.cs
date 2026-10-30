using Microsoft.Extensions.Logging;

namespace PSPad.App.Auth;

public sealed class LocalSignOut(
    ILocalSessionStore sessions,
    LocalDeviceData data,
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

    public Task ClearStorageAsync() => data.ClearAsync("sign-out");
}
