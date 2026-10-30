using PSPad.TestInfrastructure;
using PSPad.Module.Investments.Import;
using PSPad.Module.Investments.Tests.Import.Xlsx;

namespace PSPad.Module.Investments.Tests.Import;

[UnitTest]
public class XtbParserTests
{
    const double Time = 46300.5;

    static XtbFile Parse(params (string Name, object?[][] Rows)[] sheets) =>
        XtbParser.Parse(XlsxReader.Read(XlsxBuilder.Book(sheets)));

    static XtbFile ParseRows(params object?[][] rows) =>
        Parse(("Cash Operations", XtbSheets.CashOperations("12345678", rows)));

    static object?[] Deposit(double amount = 100, object? id = null) =>
        XtbSheets.Row("Deposit", null, null, Time, amount, id ?? "1", null, "IKE", null);

    [Fact]
    public void TheCashOperationsSheetIsFoundByItsHeaderNotItsName()
    {
        var file = Parse(("Operacje gotówkowe", XtbSheets.CashOperations("12345678",
            XtbSheets.Row("Stock purchase", "Acme Works SA", "ACM.PL", Time, -30.0, "900000001",
                "OPEN BUY ACM.PL 20 @ 1.5", "IKE", "800000001"))));

        Assert.Equal("12345678", file.AccountNumber);
        var row = Assert.Single(file.Rows);
        Assert.Equal(XtbRowKind.Buy, row.Kind);
        Assert.Equal(30m, row.Amount);
        Assert.Equal(20m, row.Quantity);
        Assert.Equal(1.5m, row.Price);
        Assert.Equal("Acme Works SA", row.Instrument);
        Assert.Equal("ACM.PL", row.Ticker);
        Assert.Equal("800000001", row.PositionId);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero), row.At);
        Assert.Empty(row.Errors);
    }

    [Fact]
    public void ProductComesFromTheRows()
    {
        Assert.Equal("IKE", ParseRows(Deposit()).Product);
    }

    [Fact]
    public void NumericAndStringIdsBothBecomeText()
    {
        var file = ParseRows(Deposit(id: 900000001.0), Deposit(id: "42"), Deposit(id: new InlineText("43")));

        Assert.Equal(["900000001", "42", "43"], file.Rows.Select(r => r.Id));
        Assert.All(file.Rows, r => Assert.Empty(r.Errors));
    }

    [Fact]
    public void ColumnsAreFoundByHeaderInAnyOrder()
    {
        object?[] header = ["ID", "Amount", "Type", "Time", "Product", "Position ID", "Comment", "Ticker", "Instrument"];
        object?[] row = ["7", 50.0, "Deposit", Time, "IKE", null, null, null, null];

        var file = Parse(("S", [["Account number", "1"], header, row]));

        var parsed = Assert.Single(file.Rows);
        Assert.Equal(XtbRowKind.Deposit, parsed.Kind);
        Assert.Equal(50m, parsed.Amount);
        Assert.Equal("7", parsed.Id);
        Assert.Equal(3, parsed.SheetRow);
        Assert.Empty(parsed.Errors);
    }

    [Fact]
    public void AnUnknownTypeIsARowErrorAndTheOtherRowsStillParse()
    {
        var file = ParseRows(
            XtbSheets.Row("Stock split", "X", "X.US", Time, 5, "1", null, "IKE", null),
            Deposit());

        Assert.Equal(["Unknown type 'Stock split'."], file.Rows[0].Errors);
        Assert.Null(file.Rows[0].Kind);
        Assert.Empty(file.Rows[1].Errors);
        Assert.Equal(6, file.Rows[0].SheetRow);
        Assert.Equal(7, file.Rows[1].SheetRow);
    }

    [Theory]
    [InlineData("Stock purchase", XtbRowKind.Buy)]
    [InlineData("stock sell", XtbRowKind.Sell)]
    [InlineData("Deposit", XtbRowKind.Deposit)]
    [InlineData("IKE deposit", XtbRowKind.Deposit)]
    [InlineData("IKZE deposit", XtbRowKind.Deposit)]
    [InlineData("Withdrawal", XtbRowKind.Withdrawal)]
    [InlineData("Dividend", XtbRowKind.Dividend)]
    [InlineData("Withholding tax", XtbRowKind.Tax)]
    [InlineData("Free funds interest", XtbRowKind.Interest)]
    [InlineData("Commission", XtbRowKind.Fee)]
    [InlineData("SEC fee", XtbRowKind.Fee)]
    public void TypesMapToKinds(string type, XtbRowKind kind) =>
        Assert.Equal(kind, XtbTypeMap.KindOf(type));

    [Fact]
    public void AnUnknownTypeMapsToNothing() =>
        Assert.Null(XtbTypeMap.KindOf("Stock split"));

    [Theory]
    [InlineData("Deposit", -10.0)]
    [InlineData("Dividend", -10.0)]
    [InlineData("Free funds interest", -0.5)]
    [InlineData("Withdrawal", 10.0)]
    [InlineData("Withholding tax", 3.0)]
    [InlineData("Commission", 1.0)]
    public void AWrongSignIsARowError(string type, double amount)
    {
        var row = Assert.Single(ParseRows(XtbSheets.Row(type, "N", "N.US", Time, amount, "1", null, "IKE", null)).Rows);

        Assert.Equal([$"The amount's sign does not match '{type}'."], row.Errors);
    }

    [Fact]
    public void ASellMustBePositiveAndABuyNegative()
    {
        var file = ParseRows(
            XtbSheets.Row("Stock sell", "N", "N.US", Time, -5, "1", "CLOSE BUY N.US 1 @ 5", "IKE", "9"),
            XtbSheets.Row("Stock purchase", "N", "N.US", Time, 5, "2", "OPEN BUY N.US 1 @ 5", "IKE", "9"));

        Assert.Equal(["The amount's sign does not match 'Stock sell'."], file.Rows[0].Errors);
        Assert.Equal(["The amount's sign does not match 'Stock purchase'."], file.Rows[1].Errors);
    }

    [Fact]
    public void ZeroOrMissingAmountIsARowError()
    {
        var file = ParseRows(
            XtbSheets.Row("Deposit", null, null, Time, 0, "1", null, "IKE", null),
            XtbSheets.Row("Deposit", null, null, Time, null, "2", null, "IKE", null));

        Assert.All(file.Rows, r => Assert.Contains("The row has no usable amount.", r.Errors));
    }

    [Fact]
    public void AMissingOrInvalidTimeIsARowError()
    {
        var file = ParseRows(
            XtbSheets.Row("Deposit", null, null, null, 10, "1", null, "IKE", null),
            XtbSheets.Row("Deposit", null, null, -3, 10, "2", null, "IKE", null));

        Assert.All(file.Rows, r => Assert.Contains("The row has no usable time.", r.Errors));
        Assert.All(file.Rows, r => Assert.Null(r.At));
    }

    [Fact]
    public void AMissingOrNonNumericIdIsARowError()
    {
        var file = ParseRows(
            XtbSheets.Row("Deposit", null, null, Time, 10, null, null, "IKE", null),
            XtbSheets.Row("Deposit", null, null, Time, 10, "abc", null, "IKE", null));

        Assert.All(file.Rows, r => Assert.Equal(["The row has no usable ID."], r.Errors));
    }

    [Fact]
    public void ABuyWithoutAPositionIdOrTickerOrCommentCollectsEveryError()
    {
        var row = Assert.Single(ParseRows(XtbSheets.Row("Stock purchase", null, null, Time, -10, "1", "garbage", "IKE", null)).Rows);

        Assert.Contains("The row has no position ID.", row.Errors);
        Assert.Contains("The comment does not say how many shares.", row.Errors);
        Assert.Contains("The row has no ticker.", row.Errors);
        Assert.Equal(3, row.Errors.Count);
    }

    [Theory]
    [InlineData("Dividend", 5.0)]
    [InlineData("Withholding tax", -5.0)]
    public void DividendAndTaxNeedATicker(string type, double amount)
    {
        var row = Assert.Single(ParseRows(XtbSheets.Row(type, null, null, Time, amount, "1", null, "IKE", null)).Rows);

        Assert.Equal(["The row has no ticker."], row.Errors);
    }

    [Fact]
    public void AnEmptyPositionIdOnATaxRowIsFine()
    {
        var row = Assert.Single(ParseRows(
            XtbSheets.Row("Withholding tax", "Rst Pharma", "RST.US", Time, -3.0, "5", "RST.US USD WHT 27%", "IKE", null)).Rows);

        Assert.Empty(row.Errors);
        Assert.Null(row.PositionId);
        Assert.Equal(3m, row.Amount);
    }

    [Fact]
    public void AmountsAreAbsoluteAndRoundedToTwoDecimals()
    {
        var row = Assert.Single(ParseRows(XtbSheets.Row("Withdrawal", null, null, Time, -12.3456, "1", null, "IKE", null)).Rows);

        Assert.Equal(12.35m, row.Amount);
    }

    [Fact]
    public void TotalAndBlankRowsAreSkipped()
    {
        var file = ParseRows(Deposit(), [], Deposit(id: "2"));

        Assert.Equal(2, file.Rows.Count);
        Assert.DoesNotContain(file.Rows, r => r.Type == "Total");
    }

    [Fact]
    public void AccountNumberComesFromAboveTheHeaderOfTheCashSheetOnly()
    {
        var file = Parse(
            ("Open Positions", [["Account number", "999"], ["Position", "Symbol", "Volume"], ["5", "X.PL", 2.0]]),
            ("Cash", XtbSheets.CashOperations("12345678", Deposit())));

        Assert.Equal("12345678", file.AccountNumber);
    }

    [Fact]
    public void ANumericAccountNumberIsText()
    {
        var file = Parse(("Cash", [["Account number", 12345678.0], XtbSheets.Header, Deposit()]));

        Assert.Equal("12345678", file.AccountNumber);
    }

    [Fact]
    public void AMissingAccountNumberIsAFileError()
    {
        var error = Assert.Throws<XtbFileError>(() => Parse(("Cash", [XtbSheets.Header, Deposit()])));

        Assert.Equal("This file has no account number.", error.Message);
    }

    [Fact]
    public void AFileWithoutCashOperationsIsAFileError()
    {
        var error = Assert.Throws<XtbFileError>(() => Parse(("Other", [["a", "b"]])));

        Assert.Equal("This file has no Cash Operations sheet.", error.Message);
    }

    [Fact]
    public void AHeaderMissingAColumnIsNotCashOperations()
    {
        object?[] header = ["Type", "Instrument", "Ticker", "Time", "Amount", "ID", "Comment", "Product"];

        var error = Assert.Throws<XtbFileError>(() => Parse(("Cash", [["Account number", "1"], header])));

        Assert.Equal("This file has no Cash Operations sheet.", error.Message);
    }

    [Fact]
    public void MoreThanTwentyThousandRowsIsAFileError()
    {
        var rows = Enumerable.Range(1, XtbParser.MaxRows + 1).Select(i => Deposit(id: i)).ToArray();

        var error = Assert.Throws<XtbFileError>(() => ParseRows(rows));

        Assert.Equal("This file has more than 20,000 rows. Export a shorter period.", error.Message);
    }

    [Fact]
    public void ExactlyTwentyThousandRowsParse()
    {
        var rows = Enumerable.Range(1, XtbParser.MaxRows).Select(i => Deposit(id: i)).ToArray();

        Assert.Equal(XtbParser.MaxRows, ParseRows(rows).Rows.Count);
    }

    [Fact]
    public void WithoutAnOpenPositionsSheetThereAreNone() =>
        Assert.Null(ParseRows(Deposit()).OpenPositions);

    [Fact]
    public void OpenPositionsAreFoundByTheirHeader()
    {
        var file = Parse(
            ("Cash", XtbSheets.CashOperations("12345678", Deposit())),
            ("Otwarte", [
                ["Account number", "999"],
                ["Position", "Symbol", "Volume", "Open price"],
                [800000001.0, "ACM.PL", 20.0, 1.5],
                [null, "X.PL", 1.0],
                ["77", "Y.PL", 2.5],
                ["88", "Z.PL", null],
            ]));

        Assert.Equal(
            [new XtbOpenPosition("800000001", "ACM.PL", 20m), new XtbOpenPosition("77", "Y.PL", 2.5m)],
            file.OpenPositions);
    }
}
