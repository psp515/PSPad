using PSPad.Module.Money.Import;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Money.Tests.Import;

[UnitTest]
public class CsvValuesTests
{
    [Theory]
    [InlineData("84.20", 84.20)]
    [InlineData("84,20", 84.20)]
    [InlineData("1 234,56", 1234.56)]
    [InlineData("1 234.5", 1234.5)]
    [InlineData("-3", -3)]
    public void AmountsReadEitherDecimalSeparator(string text, double expected)
    {
        Assert.Equal((decimal)expected, CsvValues.Amount(text));
    }

    [Theory]
    [InlineData("1,234.56")]
    [InlineData("1.005")]
    [InlineData("abc")]
    [InlineData("")]
    public void ABadAmountIsRejected(string text)
    {
        var error = Assert.Throws<FormatException>(() => CsvValues.Amount(text));
        Assert.Equal("Amount must be a number with at most 2 decimals.", error.Message);
    }

    [Fact]
    public void RatesTakeSixDecimalsAndEmptyMeansNone()
    {
        Assert.Equal(4.273125m, CsvValues.Rate("4,273125"));
        Assert.Null(CsvValues.Rate(" "));
        Assert.Throws<FormatException>(() => CsvValues.Rate("4.1234567"));
    }

    [Theory]
    [InlineData("2026-09-03")]
    [InlineData("03.09.2026")]
    public void DatesReadBothForms(string text)
    {
        Assert.Equal(new DateOnly(2026, 9, 3), CsvValues.Date(text));
    }

    [Fact]
    public void ABadDateIsRejected()
    {
        Assert.Equal("Date must be yyyy-MM-dd or dd.MM.yyyy.",
            Assert.Throws<FormatException>(() => CsvValues.Date("9/3/2026")).Message);
    }

    [Theory]
    [InlineData("2026-09")]
    [InlineData("09.2026")]
    public void MonthsReadBothForms(string text)
    {
        Assert.Equal(new YearMonth(2026, 9), CsvValues.Month(text));
    }

    [Fact]
    public void CurrencyIsUpperCasedAndEmptyMeansDefault()
    {
        Assert.Equal("EUR", CsvValues.Currency(" eur "));
        Assert.Null(CsvValues.Currency(""));
    }
}
