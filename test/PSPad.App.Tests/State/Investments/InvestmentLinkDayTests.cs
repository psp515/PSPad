using PSPad.App.State.Investments;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class InvestmentLinkDayTests
{
    static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void TheCurrentMonthDefaultsToToday() =>
        Assert.Equal(Today, InvestmentLinkDay.DefaultFor(new YearMonth(2026, 10), Today));

    [Fact]
    public void AnEndedMonthDefaultsToItsLastDay() =>
        Assert.Equal(new DateOnly(2026, 9, 30), InvestmentLinkDay.DefaultFor(new YearMonth(2026, 9), Today));

    [Fact]
    public void AFutureMonthNeverDefaultsPastToday() =>
        Assert.True(InvestmentLinkDay.DefaultFor(new YearMonth(2026, 12), Today) <= Today);

    [Theory]
    [InlineData(2026, 10, 1, true)]
    [InlineData(2026, 10, 9, true)]
    [InlineData(2026, 10, 10, false)]
    [InlineData(2026, 9, 30, false)]
    public void ADayMustLieInTheMonthAndNotAfterToday(int year, int month, int day, bool allowed) =>
        Assert.Equal(allowed, InvestmentLinkDay.Allowed(new YearMonth(2026, 10), new DateOnly(year, month, day), Today));

    [Theory]
    [InlineData(2026, 9, true)]
    [InlineData(2026, 10, true)]
    [InlineData(2026, 11, false)]
    public void OnlyMonthsUpToTheCurrentOneOfferTheLink(int year, int month, bool offered) =>
        Assert.Equal(offered, InvestmentLinkDay.Offered(new YearMonth(year, month), Today));
}
