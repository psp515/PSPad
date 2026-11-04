using PSPad.Module.Investments.Import;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Tests.Import.Xlsx;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Investments.Tests.Import;

[UnitTest]
public class XtbTransferRowTests
{
    const double Time = 46300.5;
    const string Main = "12345678";
    const string Plans = "87654321";

    static XtbFile ParseRows(params object?[][] rows) =>
        XtbParser.Parse(XlsxReader.Read(XlsxBuilder.Book(("Cash Operations", XtbSheets.CashOperations(Main, rows)))));

    static XtbRow Only(params object?[] row) => Assert.Single(ParseRows(row).Rows);

    static object?[] Row(string type, double amount, string comment, string product = "My Trades", object? id = null) =>
        XtbSheets.Row(type, null, null, Time, amount, id ?? "1", comment, product, null);

    [Fact]
    public void AnIkeTransferOutNamesTheIkeAccount()
    {
        var row = Only(Row("IKE deposit", -500, "Transfer out operation on account with id 55551111"));

        Assert.Equal(XtbRowKind.TransferOut, row.Kind);
        Assert.Equal(TransferType.Ike, row.TransferType);
        Assert.Equal("55551111", row.Counterparty);
        Assert.Equal(500m, row.Amount);
        Assert.Null(row.CounterAmount);
        Assert.Empty(row.Errors);
    }

    [Fact]
    public void AnIkeTransferInNamesTheSendingAccount()
    {
        var row = Only(Row("IKE deposit", 500, "Transfer in operation on account with id 55552222"));

        Assert.Equal(XtbRowKind.TransferIn, row.Kind);
        Assert.Equal("55552222", row.Counterparty);
    }

    [Fact]
    public void AnIkeCommentNamingThisAccountLeavesTheCounterpartyOut()
    {
        var row = Only(Row("IKE deposit", 500, $"Transfer in operation on account with id {Main}"));

        Assert.Equal(XtbRowKind.TransferIn, row.Kind);
        Assert.Equal(TransferType.Ike, row.TransferType);
        Assert.Null(row.Counterparty);
    }

    [Fact]
    public void ASubaccountTransferNamesTheOtherSide()
    {
        var file = ParseRows(
            Row("Subaccount transfer", -40, $"Transfer from {Main} to {Plans}", id: "1"),
            Row("Subaccount transfer", 40, $"Transfer from {Main} to {Plans}", "Investment Plans", "2"));

        Assert.Equal((XtbRowKind.TransferOut, Plans), (file.Rows[0].Kind, file.Rows[0].Counterparty));
        Assert.Equal((XtbRowKind.TransferIn, Main), (file.Rows[1].Kind, file.Rows[1].Counterparty));
        Assert.All(file.Rows, row => Assert.Equal(TransferType.Subaccount, row.TransferType));
    }

    [Fact]
    public void AConversionOutLegCountsTheTargetAmount()
    {
        var row = Only(Row("Transfer", -1000,
            "Currency conversion, PLN to USD from TA: 12345678 to: 55553333, Exchange rate:0.271234"));

        Assert.Equal(XtbRowKind.TransferOut, row.Kind);
        Assert.Equal(TransferType.CurrencyConversion, row.TransferType);
        Assert.Equal("55553333", row.Counterparty);
        Assert.Equal(271.23m, row.CounterAmount);
        Assert.Equal("USD", row.CounterCurrency);
    }

    [Fact]
    public void AConversionInLegCountsTheSourceAmount()
    {
        var row = Only(Row("Transfer", 271.23,
            "Currency conversion, PLN to USD from TA: 12345678 to: 55553333, Exchange rate:0.271234"));

        Assert.Equal(XtbRowKind.TransferIn, row.Kind);
        Assert.Equal("12345678", row.Counterparty);
        Assert.Equal(999.99m, row.CounterAmount);
        Assert.Equal("PLN", row.CounterCurrency);
    }

    [Fact]
    public void AConversionCommentThatDoesNotParseLeavesTheCounterSideOut()
    {
        var row = Only(Row("Transfer", 100, "Something else"));

        Assert.Equal(XtbRowKind.TransferIn, row.Kind);
        Assert.Null(row.Counterparty);
        Assert.Null(row.CounterAmount);
        Assert.Null(row.CounterCurrency);
        Assert.Empty(row.Errors);
    }

    [Fact]
    public void APositiveWithholdingTaxIsARefund()
    {
        var row = Only(XtbSheets.Row("Withholding tax", "Rst Pharma", "RST.US", Time, 1.25, "7",
            "US Dividends Reclassification 2025", "My Trades", null));

        Assert.Equal(XtbRowKind.TaxRefund, row.Kind);
        Assert.Empty(row.Errors);
    }

    [Fact]
    public void EveryProductOfASubaccountTransferGetsItsNumber()
    {
        var file = ParseRows(
            Row("Deposit", 100, "deposit", id: "1"),
            Row("Subaccount transfer", -40, $"Transfer from {Main} to {Plans}", id: "2"),
            Row("Subaccount transfer", 40, $"Transfer from {Main} to {Plans}", "Investment Plans", "3"),
            Row("Deposit", 5, "deposit", "Other Product", "4"));

        Assert.Equal([Main, Main, Plans, Main], file.Rows.Select(file.AccountNumberOf));
        Assert.Equal([Main, Plans], file.AccountNumbers);
    }

    [Fact]
    public void ATransferBackGivesTheSameNumbers()
    {
        var file = ParseRows(
            Row("Subaccount transfer", 25, $"Transfer from {Plans} to {Main}", id: "1"),
            Row("Subaccount transfer", -25, $"Transfer from {Plans} to {Main}", "Investment Plans", "2"));

        Assert.Equal([Main, Plans], file.Rows.Select(file.AccountNumberOf));
    }

    [Fact]
    public void AFileWithoutSubaccountTransfersFeedsOneAccount()
    {
        var file = ParseRows(Row("Deposit", 100, "deposit", "Investment Plans"));

        Assert.Equal([Main], file.AccountNumbers);
        Assert.Equal("Investment Plans", file.ProductOf(Main));
    }

    [Fact]
    public void EachNumberTakesTheProductOfItsRows()
    {
        var file = ParseRows(
            Row("Subaccount transfer", -40, $"Transfer from {Main} to {Plans}", id: "1"),
            Row("Subaccount transfer", 40, $"Transfer from {Main} to {Plans}", "Investment Plans", "2"));

        Assert.Equal("My Trades", file.ProductOf(Main));
        Assert.Equal("Investment Plans", file.ProductOf(Plans));
    }
}
