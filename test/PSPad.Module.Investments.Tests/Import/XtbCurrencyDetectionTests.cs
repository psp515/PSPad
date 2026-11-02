using PSPad.Module.Investments.Import;
using PSPad.Module.Investments.Tests.Import.Xlsx;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Investments.Tests.Import;

[UnitTest]
public class XtbCurrencyDetectionTests
{
    const double Time = 46300.5;
    const string Main = "12345678";
    const string Conversion = "Currency conversion, PLN to USD from TA: 12345678 to: 55553333, Exchange rate:0.271234";
    const string IntoThisAccount = "Currency conversion, PLN to USD from TA: 55553333 to: 12345678, Exchange rate:0.271234";

    static object?[] Row(string type, double amount, string? comment = null, string product = "My Trades", string id = "1") =>
        XtbSheets.Row(type, null, null, Time, amount, id, comment, product, null);

    static object?[][] Summary(params (string Product, string Currency)[] rows) =>
        [
            ["Account number", Main],
            ["Product", "Metric", "Amount", "Currency"],
            .. rows.Select(row => new object?[] { row.Product, "Open position value", 1000.0, row.Currency }),
            ["Product", "Instrument/Position", "Ticker", "Category", "Type", "Volume", "Value"],
        ];

    static XtbFile Parse(object?[][] cash, object?[][]? openPositions = null) =>
        XtbParser.Parse(XlsxReader.Read(openPositions is null
            ? XlsxBuilder.Book(("Cash Operations", cash))
            : XlsxBuilder.Book(("Cash Operations", cash), ("Open Positions", openPositions))));

    [Fact]
    public void TheOpenPositionsSummaryNamesTheCurrencyOfEachProduct()
    {
        var file = Parse(XtbSheets.CashOperations(Main, Row("Deposit", 100)), Summary(("My Trades", "EUR")));

        Assert.Equal("EUR", file.CurrencyOf(Main));
    }

    [Fact]
    public void AnOutgoingConversionIsInTheAccountsOwnFromCurrency() =>
        Assert.Equal("PLN", Parse(XtbSheets.CashOperations(Main, Row("Transfer", -1000, Conversion))).CurrencyOf(Main));

    [Fact]
    public void AnIncomingConversionIsInTheAccountsOwnToCurrency() =>
        Assert.Equal("USD", Parse(XtbSheets.CashOperations(Main, Row("Transfer", 271.23, IntoThisAccount))).CurrencyOf(Main));

    [Fact]
    public void TheSummaryWinsOverAConversionComment()
    {
        var file = Parse(XtbSheets.CashOperations(Main, Row("Transfer", 271.23, IntoThisAccount)), Summary(("My Trades", "EUR")));

        Assert.Equal("EUR", file.CurrencyOf(Main));
    }

    [Fact]
    public void ASummaryRowInAnUnknownCurrencyDoesNotHideTheRowsAfterIt()
    {
        var file = Parse(XtbSheets.CashOperations(Main, Row("Deposit", 100)), Summary(("Other", "XYZ"), ("My Trades", "EUR")));

        Assert.Equal("EUR", file.CurrencyOf(Main));
    }

    [Fact]
    public void ASummaryWithNoRowsSaysNothing()
    {
        var file = Parse(XtbSheets.CashOperations(Main, Row("Deposit", 100)), Summary());

        Assert.Null(file.CurrencyOf(Main));
    }

    [Fact]
    public void WithNothingInTheFileTheFileNameDecidesForTheMainAccount()
    {
        var file = Parse(XtbSheets.CashOperations(Main, Row("Deposit", 100))).NamedAfter("EUR_12345678_2006-01-01_2026-11-02.xlsx");

        Assert.Equal("EUR", file.CurrencyOf(Main));
        Assert.Null(file.CurrencyOf("87654321"));
    }

    [Theory]
    [InlineData("IKE_12345678_2006-01-01_2026-11-02.xlsx")]
    [InlineData("statement.xlsx")]
    [InlineData("XYZ_12345678.xlsx")]
    [InlineData(null)]
    public void AFileNameWithoutAKnownCurrencySaysNothing(string? name) =>
        Assert.Null(Parse(XtbSheets.CashOperations(Main, Row("Deposit", 100))).NamedAfter(name).CurrencyOf(Main));

    [Fact]
    public void TheFileWinsOverItsName()
    {
        var file = Parse(XtbSheets.CashOperations(Main, Row("Deposit", 100)), Summary(("My Trades", "EUR")))
            .NamedAfter("USD_12345678_2006-01-01_2026-11-02.xlsx");

        Assert.Equal("EUR", file.CurrencyOf(Main));
    }
}
