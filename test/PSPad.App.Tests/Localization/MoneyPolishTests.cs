using System.Globalization;
using PSPad.App.Localization;
using PSPad.App.Resources;
using PSPad.App.State;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Import;
using PSPad.Module.Money.Reading;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Localization;

[UnitTest]
[Collection("Culture")]
public class MoneyPolishTests
{
    static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
    static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void AmountsUseThePolishDecimalComma() => InPolish(() =>
    {
        Assert.Equal("250,00 PLN", MoneyText.Pln(250m));
        Assert.Equal("−12,50 PLN", MoneyText.SignedPln(-12.5m));
        Assert.Equal("4,3000", MoneyText.Rate(4.3m));
    });

    [Fact]
    public void MonthsAndDaysUsePolishNames() => InPolish(() =>
    {
        Assert.Equal("październik 2026", MoneyText.MonthTitle(new YearMonth(2026, 10)));
        Assert.Equal("piątek, 9 października", MoneyText.Day(Today));
        Assert.Equal(["sty", "lut", "mar"],
            MoneyText.MonthAxis([new YearMonth(2026, 1), new YearMonth(2026, 2), new YearMonth(2026, 3)]));
    });

    [Fact]
    public void RecordedTimesSayTodayAndYesterdayInPolish() => InPolish(() =>
    {
        var text = TestLocalizers.For<BudgetsStrings>();

        Assert.Equal("dziś 20:40",
            MoneyText.Recorded(new DateTimeOffset(2026, 10, 9, 18, 40, 0, TimeSpan.Zero), Warsaw, Today, text));
        Assert.Equal("wczoraj 01:14",
            MoneyText.Recorded(new DateTimeOffset(2026, 10, 7, 23, 14, 0, TimeSpan.Zero), Warsaw, Today, text));
    });

    [Fact]
    public void InsightsAreWholePolishSentences() => InPolish(() =>
    {
        var text = TestLocalizers.For<BudgetsStrings>();
        var insight = new Insight(InsightKind.IncomeExpected, "Pensja", 300m, 0m, 28);

        Assert.Equal("Pensja zwykle wpływa około 28. dnia", InsightText.Title(insight, text));
        Assert.Equal("Średnio 300,00 PLN; w tym miesiącu jeszcze nic", InsightText.Caption(insight, text));
    });

    [Theory]
    [InlineData(1, "średnia z ostatniego miesiąca")]
    [InlineData(3, "średnia z ostatnich 3 miesięcy")]
    [InlineData(6, "średnia z ostatnich 6 miesięcy")]
    public void TheAverageWindowAgreesWithTheCount(int months, string expected) => InPolish(() =>
        Assert.Equal(expected, TestLocalizers.For<BudgetsStrings>().Plural(months, "AvgOfLast")));

    [Theory]
    [InlineData(1, "1 wpis · przychody 10,00 · wydatki 5,00 · wynik +5,00 PLN")]
    [InlineData(3, "3 wpisy · przychody 10,00 · wydatki 5,00 · wynik +5,00 PLN")]
    [InlineData(5, "5 wpisów · przychody 10,00 · wydatki 5,00 · wynik +5,00 PLN")]
    public void TheEntriesCaptionIsAWholeSentenceThatAgreesWithTheCount(int count, string expected) => InPolish(() =>
        Assert.Equal(expected, TestLocalizers.For<BudgetsStrings>().Plural(
            count, "EntriesCaption", MoneyText.Amount(10m), MoneyText.Amount(5m), MoneyText.SignedPln(5m))));

    [Theory]
    [InlineData(HoldingType.Cash, "Gotówka")]
    [InlineData(HoldingType.Savings, "Oszczędności")]
    [InlineData(HoldingType.Investment, "Inwestycje")]
    public void HoldingTypesSpeakPolish(HoldingType type, string expected) => InPolish(() =>
        Assert.Equal(expected, TestLocalizers.For<BudgetsStrings>().Of(type)));

    [Fact]
    public void ImportStatusesSpeakPolish() => InPolish(() =>
    {
        var text = TestLocalizers.For<BudgetsStrings>();

        Assert.Equal("Do zaimportowania", ImportText.StatusText(ImportStatus.ToImport, text));
        Assert.Equal("Błąd", ImportText.StatusText(ImportStatus.Error, text));
        Assert.Equal("Pobieranie kursu…", ImportText.StatusText(ImportStatus.WaitingForRate, text));
    });

    static void InPolish(Action assertion)
    {
        var before = CultureInfo.CurrentUICulture;
        var beforeFormat = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            assertion();
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
            CultureInfo.CurrentCulture = beforeFormat;
        }
    }
}
