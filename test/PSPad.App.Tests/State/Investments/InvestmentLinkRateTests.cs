using PSPad.App.State.Investments;
using PSPad.Contracts;
using PSPad.Module.Money.Reading;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class InvestmentLinkRateTests
{
    static readonly DateOnly Day = new(2026, 10, 29);

    [Fact]
    public void AnNbpRateWinsAndKeepsItsEffectiveDate()
    {
        var rate = InvestmentLinkRate.Choose("USD", Day, new NbpRateView("USD", 3.64m, new DateOnly(2026, 10, 28)),
            new RateSuggestion(3.5m, new DateOnly(2026, 9, 1)));

        Assert.Equal(new LinkRate(3.64m, new DateOnly(2026, 10, 28)), rate);
    }

    [Fact]
    public void AnNbpAnswerForAnotherCurrencyIsIgnored()
    {
        var rate = InvestmentLinkRate.Choose("USD", Day, new NbpRateView("EUR", 4.25m, Day), new RateSuggestion(3.5m, new DateOnly(2026, 9, 1)));

        Assert.Equal(new LinkRate(3.5m, new DateOnly(2026, 9, 1)), rate);
    }

    [Fact]
    public void WithoutNbpTheSuggestionIsUsedWithItsOwnDate()
    {
        var rate = InvestmentLinkRate.Choose("USD", Day, null, new RateSuggestion(3.5m, new DateOnly(2026, 9, 1)));

        Assert.Equal(new LinkRate(3.5m, new DateOnly(2026, 9, 1)), rate);
    }

    [Fact]
    public void ASuggestionWithoutADateIsDatedTheDay()
    {
        Assert.Equal(new LinkRate(3.5m, Day), InvestmentLinkRate.Choose("USD", Day, null, new RateSuggestion(3.5m, null)));
    }

    [Fact]
    public void NeitherSourceLeavesTheRateToBeTyped() =>
        Assert.Equal(new LinkRate(null, Day), InvestmentLinkRate.Choose("USD", Day, null, null));

    [Fact]
    public void ZlotyNeedsNoRate() =>
        Assert.Equal(new LinkRate(1m, Day), InvestmentLinkRate.Choose("PLN", Day, null, null));

    [Fact]
    public void ATypedRateIsDatedTheDay() =>
        Assert.Equal(new LinkRate(3.7m, Day), InvestmentLinkRate.Typed(3.7m, Day));
}
