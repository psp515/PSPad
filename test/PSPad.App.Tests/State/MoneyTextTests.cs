using System.Globalization;
using PSPad.App.State;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class MoneyTextTests
{
    [Fact]
    public void PlnHasTwoDecimalsAndGroupSeparators() => Assert.Equal("1,234.50 PLN", MoneyText.Pln(1234.5m));

    [Theory]
    [InlineData(-12, "−12.00 PLN")]
    [InlineData(5, "+5.00 PLN")]
    [InlineData(0, "0.00 PLN")]
    public void SignedPlnShowsTheSign(int value, string expected) => Assert.Equal(expected, MoneyText.SignedPln(value));

    [Theory]
    [InlineData(-1234.5, "−1,234.50")]
    [InlineData(5, "+5.00")]
    [InlineData(0, "0.00")]
    public void SignedAmountShowsTheSignWithoutACurrency(double value, string expected) =>
        Assert.Equal(expected, MoneyText.SignedAmount((decimal)value));

    [Fact]
    public void AnExpenseIsMinusAndAnIncomeIsPlus()
    {
        Assert.Equal("−10.00 EUR", MoneyText.Signed(CategoryKind.Expense, 10m, "EUR"));
        Assert.Equal("+10.00 EUR", MoneyText.Signed(CategoryKind.Income, 10m, "EUR"));
    }

    [Fact]
    public void ARateKeepsAtLeastFourDecimals()
    {
        Assert.Equal("4.3000", MoneyText.Rate(4.3m));
        Assert.Equal("4.251234", MoneyText.Rate(4.251234m));
    }

    [Fact]
    public void AMonthReadsAsNameAndYear() => Assert.Equal("October 2026", MoneyText.MonthTitle(new YearMonth(2026, 10)));

    [Fact]
    public void ADayReadsAsWeekdayAndDate() => Assert.Equal("Friday, 9 October", MoneyText.Day(new DateOnly(2026, 10, 9)));

    [Theory]
    [InlineData("0.25", "25%")]
    [InlineData("0.857", "86%")]
    [InlineData("0", "0%")]
    [InlineData("-0.125", "−13%")]
    [InlineData("-0.004", "0%")]
    public void ASavingsRateIsAWholePercent(string share, string expected) =>
        Assert.Equal(expected, MoneyText.Percent(decimal.Parse(share, CultureInfo.InvariantCulture)));

    [Fact]
    public void NoSavingsRateIsADash() => Assert.Equal("—", MoneyText.Percent(null));

    [Fact]
    public void ARangeReadsFromDayToDay() =>
        Assert.Equal("1 Jan 2026 – 9 Oct 2026", MoneyText.Range(new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 9)));

    [Fact]
    public void MonthsOfOneYearAreLabelledByName() =>
        Assert.Equal(["Jan", "Feb", "Mar"],
            MoneyText.MonthAxis([new YearMonth(2026, 1), new YearMonth(2026, 2), new YearMonth(2026, 3)]));

    [Fact]
    public void MonthsAcrossAYearEndCarryTheYearAndAreThinned()
    {
        var months = Enumerable.Range(0, 12).Select(offset => YearMonth.Of(new DateOnly(2025, 11, 1).AddMonths(offset))).ToArray();

        Assert.Equal(
            ["Nov 2025", "", "Jan 2026", "", "Mar 2026", "", "May 2026", "", "Jul 2026", "", "Sep 2026", ""],
            MoneyText.MonthAxis(months));
    }

    [Theory]
    [InlineData(0, "0 months")]
    [InlineData(1, "1 month")]
    [InlineData(6, "6 months")]
    public void AMonthCountAgreesWithItsNumber(int months, string expected) => Assert.Equal(expected, MoneyText.MonthCount(months));

    [Fact]
    public void NoMonthsHaveNoLabels() => Assert.Empty(MoneyText.MonthAxis([]));

    static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
    static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void RecordedTodayShowsTheTimeInTheUsersZone() =>
        Assert.Equal("today 20:40", MoneyText.Recorded(new DateTimeOffset(2026, 10, 9, 18, 40, 0, TimeSpan.Zero), Warsaw, Today));

    [Fact]
    public void RecordedYesterdaySaysSo() =>
        Assert.Equal("yesterday 01:14", MoneyText.Recorded(new DateTimeOffset(2026, 10, 7, 23, 14, 0, TimeSpan.Zero), Warsaw, Today));

    [Fact]
    public void RecordedEarlierThisYearShowsTheDay() =>
        Assert.Equal("3 Oct 11:00", MoneyText.Recorded(new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero), Warsaw, Today));

    [Fact]
    public void RecordedInAnotherYearShowsTheYear() =>
        Assert.Equal("3 Oct 2025 11:00", MoneyText.Recorded(new DateTimeOffset(2025, 10, 3, 9, 0, 0, TimeSpan.Zero), Warsaw, Today));
}
