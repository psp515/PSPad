using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PSPad.Api.Identity;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Identity;

[UnitTest]
public class ClaimsCurrentUserTests
{
    [Fact]
    public void ARawNameClaimIsTheDisplayName()
    {
        var user = CurrentUserWith(new Claim("name", "Ada Lovelace"));

        Assert.Equal("Ada Lovelace", user.DisplayName);
    }

    [Fact]
    public void AMappedNameClaimIsTheDisplayNameWhenNoRawNameClaimExists()
    {
        var user = CurrentUserWith(new Claim(ClaimTypes.Name, "Ada Lovelace"));

        Assert.Equal("Ada Lovelace", user.DisplayName);
    }

    [Fact]
    public void APreferredUsernameClaimIsTheDisplayNameWhenNoNameClaimExists()
    {
        var user = CurrentUserWith(new Claim("preferred_username", "ada"));

        Assert.Equal("ada", user.DisplayName);
    }

    [Fact]
    public void TheSubjectIsTheDisplayNameWhenNoneOfTheNameClaimsExist()
    {
        var subject = Guid.NewGuid().ToString();

        var user = CurrentUserWith(new Claim("sub", subject));

        Assert.Equal(subject, user.DisplayName);
    }

    [Fact]
    public void AnEmailClaimIsTheEmail()
    {
        var user = CurrentUserWith(new Claim("email", "ada@example.com"));

        Assert.Equal("ada@example.com", user.Email);
    }

    [Fact]
    public void EmailIsEmptyWhenNoEmailClaimExists()
    {
        var user = CurrentUserWith(new Claim("sub", Guid.NewGuid().ToString()));

        Assert.Equal("", user.Email);
    }

    static ClaimsCurrentUser CurrentUserWith(params Claim[] claims)
    {
        var hasSubject = claims.Any(claim => claim.Type is "sub" or ClaimTypes.NameIdentifier);
        var allClaims = hasSubject
            ? claims
            : [.. claims, new Claim("sub", Guid.NewGuid().ToString())];

        var identity = new ClaimsIdentity(allClaims, "Test");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        return new ClaimsCurrentUser(new StubHttpContextAccessor(httpContext));
    }
}
