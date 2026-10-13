using System.Security.Claims;
using PSPad.Api.Identity;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Identity;

[UnitTest]
public class RecentAuthenticationTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);

    [Theory]
    [InlineData(0, true)]
    [InlineData(299, true)]
    [InlineData(300, true)]
    [InlineData(301, false)]
    [InlineData(-30, true)]
    public void ASignInCountsAsRecentUpToTheMaximumAge(int secondsAgo, bool recent)
    {
        var user = SignedInAt(Now.AddSeconds(-secondsAgo).ToUnixTimeSeconds().ToString());

        Assert.Equal(recent, RecentAuthentication.Holds(user, Now, FiveMinutes));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("yesterday")]
    public void ASignInTimeThatIsMissingOrUnreadableIsNeverRecent(string? authTime)
    {
        Assert.False(RecentAuthentication.Holds(SignedInAt(authTime), Now, FiveMinutes));
    }

    static ClaimsPrincipal SignedInAt(string? authTime)
    {
        var claims = new List<Claim> { new("sub", "someone") };
        if (authTime is not null)
        {
            claims.Add(new Claim("auth_time", authTime));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
