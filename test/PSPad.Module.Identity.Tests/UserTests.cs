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

    [Fact]
    public void ProvisioningRecordsTheLanguage()
    {
        var user = new User();
        user.ApplyAll(User.Decide(null, Command("Europe/Warsaw", "pl"), Now));

        Assert.Equal("pl", user.Language);
    }

    [Fact]
    public void ProvisioningWithAnUnsupportedLanguageIsRejected()
    {
        Assert.Throws<DomainRejectedException>(() => User.Decide(null, Command("Europe/Warsaw", "de"), Now));
    }

    [Fact]
    public void ADocumentWithoutALanguageSpeaksEnglish()
    {
        Assert.Equal("en", new User().Language);
    }

    [Fact]
    public void ChangingTheLanguageKeepsTheUser()
    {
        var user = Provisioned("Europe/Warsaw");

        user.ApplyAll(User.Decide(user, new SetUserLanguage(Guid.NewGuid(), user.Id, "pl"), Now));

        Assert.Equal("pl", user.Language);
        Assert.Equal("keycloak-sub-1", user.Subject);
    }

    [Fact]
    public void SettingTheSameLanguageProducesNoEvent()
    {
        var user = Provisioned("Europe/Warsaw");

        Assert.Empty(User.Decide(user, new SetUserLanguage(Guid.NewGuid(), user.Id, "en"), Now));
    }

    [Fact]
    public void AnUnsupportedLanguageIsRejected()
    {
        var user = Provisioned("Europe/Warsaw");

        Assert.Throws<DomainRejectedException>(
            () => User.Decide(user, new SetUserLanguage(Guid.NewGuid(), user.Id, "de"), Now));
    }

    static ProvisionUser Command(string zone, string language = "en") =>
        new(Guid.NewGuid(), User.IdFor("keycloak-sub-1"), "keycloak-sub-1", "Łukasz", zone, language);

    static User Provisioned(string zone)
    {
        var user = new User();
        user.ApplyAll(User.Decide(null, Command(zone), Now));
        return user;
    }

    static User Provisioned(string subject, string displayName)
    {
        var user = new User();
        user.ApplyAll(User.Decide(
            null, new ProvisionUser(Guid.NewGuid(), Guid.Empty, subject, displayName, "Etc/UTC", "en"), Now));
        return user;
    }

    [Fact]
    public void ItSetsANewDisplayName()
    {
        var user = Provisioned("ada@example.com", "old name");

        var events = User.Decide(
            user, new SetUserDisplayName(Guid.NewGuid(), user.Id, "Ada Lovelace"), Now);

        var renamed = Assert.IsType<UserDisplayNameSet>(Assert.Single(events));
        Assert.Equal("Ada Lovelace", renamed.DisplayName);
    }

    [Fact]
    public void ItIgnoresADisplayNameThatHasNotChanged()
    {
        var user = Provisioned("ada@example.com", "Ada Lovelace");

        var events = User.Decide(
            user, new SetUserDisplayName(Guid.NewGuid(), user.Id, "Ada Lovelace"), Now);

        Assert.Empty(events);
    }

    [Fact]
    public void ItRejectsABlankDisplayName()
    {
        var user = Provisioned("ada@example.com", "Ada Lovelace");

        Assert.Throws<DomainRejectedException>(() => User.Decide(
            user, new SetUserDisplayName(Guid.NewGuid(), user.Id, "   "), Now));
    }
}
