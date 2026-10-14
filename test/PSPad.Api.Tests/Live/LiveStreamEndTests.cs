using System.Globalization;
using System.Security.Claims;
using PSPad.Api.Live;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Live;

[UnitTest]
public class LiveStreamEndTests
{
    static readonly DateTimeOffset Opened = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    static readonly TimeSpan Max = TimeSpan.FromMinutes(15);

    static ClaimsPrincipal Expiring(DateTimeOffset at) =>
        new(new ClaimsIdentity([new Claim("exp", at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))], "Test"));

    [Fact]
    public void ATokenExpiringFirstEndsTheStreamThen() =>
        Assert.Equal(Opened.AddMinutes(5), LiveStreamEnd.For(Expiring(Opened.AddMinutes(5)), Opened, Max));

    [Fact]
    public void ATokenOutlivingTheMaximumEndsTheStreamAtTheMaximum() =>
        Assert.Equal(Opened + Max, LiveStreamEnd.For(Expiring(Opened.AddHours(1)), Opened, Max));

    [Fact]
    public void ATokenWithoutExpiryEndsTheStreamAtTheMaximum() =>
        Assert.Equal(Opened + Max, LiveStreamEnd.For(new ClaimsPrincipal(new ClaimsIdentity()), Opened, Max));
}
