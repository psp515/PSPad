using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace PSPad.App.Auth;

public sealed class LocalAuthenticationStateProvider(ILocalSessionStore sessions)
    : AuthenticationStateProvider
{
    LocalSession? _session;
    bool _loaded;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_loaded)
        {
            _session = await sessions.LoadAsync();
            _loaded = true;
        }

        return new AuthenticationState(Principal(_session));
    }

    public void SignedIn(LocalSession session)
    {
        _session = session;
        _loaded = true;
        NotifyAuthenticationStateChanged(
            Task.FromResult(new AuthenticationState(Principal(session))));
    }

    public void SignedOut()
    {
        _session = null;
        _loaded = true;
        NotifyAuthenticationStateChanged(
            Task.FromResult(new AuthenticationState(Principal(null))));
    }

    static ClaimsPrincipal Principal(LocalSession? session) =>
        session is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
                    new Claim(ClaimTypes.Name, session.DisplayName),
                    new Claim(ClaimTypes.Email, session.Email)
                ],
                "pspad-local", ClaimTypes.Name, ClaimTypes.Role));
}
