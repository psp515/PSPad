using System.Net.Http.Headers;
using PSPad.Abstractions;

namespace PSPad.App.Auth;

public sealed class SessionAuthorizationHandler(
    TokenRefresher refresher, ILocalSessionStore sessions, IClock clock) : DelegatingHandler
{
    static readonly TimeSpan Margin = TimeSpan.FromSeconds(30);

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
        if (refresher.AccessToken is not null && refresher.AccessTokenExpiresAt - Margin > clock.UtcNow)
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

        return null;
    }
}
