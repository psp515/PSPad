using System.Net.Http.Headers;
using PSPad.Abstractions;

namespace PSPad.App.Auth;

public sealed class SessionAuthorizationHandler(
    TokenRefresher refresher,
    ILocalSessionStore sessions,
    IClock clock,
    LocalAuthenticationStateProvider authenticationState) : DelegatingHandler
{
    static readonly TimeSpan Margin = TimeSpan.FromSeconds(30);

    readonly SemaphoreSlim gate = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await TokenAsync();

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    async Task<string?> TokenAsync()
    {
        if (CachedTokenValid())
        {
            return refresher.AccessToken;
        }

        await gate.WaitAsync();

        try
        {
            if (CachedTokenValid())
            {
                return refresher.AccessToken;
            }

            LocalSession? session;

            try
            {
                session = await sessions.LoadAsync();
            }
            catch
            {
                return null;
            }

            if (session is null)
            {
                return null;
            }

            var outcome = await refresher.RefreshAsync(session.RefreshToken);

            if (outcome is RefreshOutcome.Renewed renewed)
            {
                await sessions.SaveAsync(session with
                {
                    RefreshToken = renewed.RefreshToken,
                    LastServerContactUtc = clock.UtcNow
                });

                return renewed.AccessToken;
            }

            if (outcome is RefreshOutcome.Revoked)
            {
                return await HandleRevokedAsync(session);
            }

            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    bool CachedTokenValid() =>
        refresher.AccessToken is not null && refresher.AccessTokenExpiresAt - Margin > clock.UtcNow;

    async Task<string?> HandleRevokedAsync(LocalSession attempted)
    {
        LocalSession? current;

        try
        {
            current = await sessions.LoadAsync();
        }
        catch
        {
            return null;
        }

        if (current is null || current.RefreshToken != attempted.RefreshToken)
        {
            return null;
        }

        try
        {
            await sessions.ClearAsync();
        }
        catch
        {
            return null;
        }

        authenticationState.SignedOut();

        return null;
    }
}
