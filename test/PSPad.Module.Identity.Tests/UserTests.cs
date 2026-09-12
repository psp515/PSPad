using PSPad.Abstractions;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Identity.Tests;

[UnitTest]
public class UserTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheSameSubjectAlwaysMapsToTheSameId()
    {
        Assert.Equal(User.IdFor("keycloak-sub-1"), User.IdFor("keycloak-sub-1"));
        Assert.NotEqual(User.IdFor("keycloak-sub-1"), User.IdFor("keycloak-sub-2"));
    }

    [Fact]
    public void ProvisioningRecordsTheSubjectAndTheTimeZone()
    {
        var user = Provisioned("Europe/Warsaw");

        Assert.Equal("keycloak-sub-1", user.Subject);
        Assert.Equal("Europe/Warsaw", user.TimeZone);
        Assert.Equal(Now, user.ProvisionedAt);
    }

    [Fact]
    public void ProvisioningTwiceProducesNoSecondEvent()
    {
        var user = Provisioned("Europe/Warsaw");

        Assert.Empty(User.Decide(user, Command("Europe/Warsaw"), Now));
    }

    [Fact]
    public void AnUnknownTimeZoneIsRejected()
    {
        Assert.Throws<DomainRejectedException>(() => User.Decide(null, Command("Mars/Olympus"), Now));
    }

    [Fact]
    public void ChangingTheTimeZoneKeepsTheUser()
    {
        var user = Provisioned("Europe/Warsaw");

        user.ApplyAll(User.Decide(
            user, new SetUserTimeZone(Guid.NewGuid(), user.Id, "Pacific/Auckland"), Now));

        Assert.Equal("Pacific/Auckland", user.TimeZone);
    }

    static ProvisionUser Command(string zone) =>
        new(Guid.NewGuid(), User.IdFor("keycloak-sub-1"), "keycloak-sub-1", "Łukasz", zone);

    static User Provisioned(string zone)
    {
        var user = new User();
        user.ApplyAll(User.Decide(null, Command(zone), Now));
        return user;
    }
}
