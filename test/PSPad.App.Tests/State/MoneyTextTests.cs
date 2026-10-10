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
}
