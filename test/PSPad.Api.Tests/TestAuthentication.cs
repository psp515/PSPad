using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PSPad.Api.Tests;

public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public new const string Scheme = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Subject", out var subject))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, subject!) };
        if (Request.Headers.TryGetValue("X-Test-Zone", out var zone))
        {
            claims.Add(new Claim("zoneinfo", zone!));
        }

        if (Request.Headers.TryGetValue("X-Test-Name", out var name))
        {
            claims.Add(new Claim("name", name!));
        }

        if (Request.Headers.TryGetValue("X-Test-Preferred-Username", out var preferred))
        {
            claims.Add(new Claim("preferred_username", preferred!));
        }

        if (Request.Headers.TryGetValue("X-Test-Email", out var email))
        {
            claims.Add(new Claim("email", email!));
        }

        var identity = new ClaimsIdentity(claims, Scheme);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}
