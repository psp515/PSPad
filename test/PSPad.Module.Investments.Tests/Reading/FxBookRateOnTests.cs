using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Investments.Tests.Reading;

[UnitTest]
public class FxBookRateOnTests
{
    static readonly FxBook Book = Market.Rates(
        ("USD", new DateOnly(2026, 3, 2), 4.0m), ("USD", new DateOnly(2026, 3, 6), 4.2m));

    [Fact]
    public void ADayWithAFixingGetsIt() =>
        Assert.Equal(new DailyRate(new DateOnly(2026, 3, 6), 4.2m), Book.RateOn("USD", new DateOnly(2026, 3, 6)));

    [Fact]
    public void ADayWithoutAFixingGetsTheLatestEarlierOne() =>
        Assert.Equal(new DailyRate(new DateOnly(2026, 3, 6), 4.2m), Book.RateOn("USD", new DateOnly(2026, 3, 8)));

    [Fact]
    public void ADayBeforeTheFirstFixingGetsNone() =>
        Assert.Null(Book.RateOn("USD", new DateOnly(2026, 3, 1)));

    [Fact]
    public void AnUnknownCurrencyGetsNone() =>
        Assert.Null(Book.RateOn("EUR", new DateOnly(2026, 3, 6)));

    [Fact]
    public void ZlotyIsOneOnTheDayItself() =>
        Assert.Equal(new DailyRate(new DateOnly(2026, 3, 8), 1m), Book.RateOn("pln", new DateOnly(2026, 3, 8)));
}
