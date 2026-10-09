using PSPad.Abstractions;
using PSPad.Module.Money.Preferences;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Money.Tests.Preferences;

[UnitTest]
public class MoneyPreferencesTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IdForIsStableAndPerUser()
    {
        Assert.Equal(MoneyPreferences.IdFor(User), MoneyPreferences.IdFor(User));
        Assert.NotEqual(MoneyPreferences.IdFor(User), MoneyPreferences.IdFor(Guid.NewGuid()));
        Assert.NotEqual(User, MoneyPreferences.IdFor(User));
    }

    [Fact]
    public void ANewDocumentDefaultsToPln() => Assert.Equal("PLN", new MoneyPreferences().DefaultCurrency);

    [Fact]
    public void SettingCreatesTheDocument()
    {
        var set = Assert.IsType<DefaultCurrencySet>(Assert.Single(
            MoneyPreferences.Decide(null, new SetDefaultCurrency(Guid.NewGuid(), User, "eur"), Now)));

        var preferences = new MoneyPreferences();
        preferences.ApplyAll([set]);

        Assert.Equal(MoneyPreferences.IdFor(User), preferences.Id);
        Assert.Equal(User, preferences.UserId);
        Assert.Equal("EUR", preferences.DefaultCurrency);
    }

    [Fact]
    public void SettingTheSameCurrencyEmitsNothing()
    {
        var preferences = new MoneyPreferences();
        preferences.ApplyAll(MoneyPreferences.Decide(null, new SetDefaultCurrency(Guid.NewGuid(), User, "EUR"), Now));

        Assert.Empty(MoneyPreferences.Decide(preferences, new SetDefaultCurrency(Guid.NewGuid(), User, "eur"), Now));
    }

    [Fact]
    public void AnUnknownCurrencyIsRejected() =>
        Assert.Throws<DomainRejectedException>(
            () => MoneyPreferences.Decide(null, new SetDefaultCurrency(Guid.NewGuid(), User, "XXX"), Now));
}
