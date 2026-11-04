using PSPad.Module.Investments.Import;
using PSPad.Module.Investments.Tests.Import.Xlsx;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Investments.Tests.Import;

[UnitTest]
public class XtbPositionSheetsTests
{
    const double Time = 46300.5;
    const double OpenTime = 46290.25;

    static readonly DateTimeOffset Opened = ExcelTime.ToUtc(OpenTime);

    static readonly object?[] ClosedHeader =
    [
        "Instrument", "Ticker", "Category", "Type", "Volume", "Open Price", "Open Time (UTC)", "Close Price",
        "Close Time (UTC)", "Product", "Profit/Loss", "Gross Profit", "Purchase Value", "Sale Value", "Stop Loss",
        "Take Profit", "Commission", "Margin", "Swap", "Rollover", "Open Conversion Rate", "Close Conversion Rate",
        "Close Origin", "Position ID", "Comment"
    ];

    static object?[] Closed(string ticker, double volume, double openPrice, object? position, string? comment) =>
    [
        "Newco SA", ticker, "STOCK", "BUY", volume, openPrice, OpenTime, 25.0, Time, "IKE", 174.99, 174.99, 0.01, 175.0,
        null, null, 0.0, null, null, null, 1.0, 1.0, "Client", position, comment
    ];

    static XtbFile Parse(params (string Name, object?[][] Rows)[] sheets) =>
        XtbParser.Parse(XlsxReader.Read(XlsxBuilder.Book(sheets)));

    static (string, object?[][]) Cash() =>
        ("Cash Operations", XtbSheets.CashOperations("12345678",
            XtbSheets.Row("Deposit", null, null, Time, 100.0, "1", null, "IKE", null)));

    [Fact]
    public void ClosedPositionsAreReadFromTheirSheetWithAFloatPositionId()
    {
        var file = Parse(Cash(), ("Closed Positions", [
            ["Account number", "12345678"],
            ["Closed Positions"],
            ClosedHeader,
            Closed("NEW.PL", 7.0, 0.002, 2.7e9, "OLD.PL spinoff"),
            Closed("ACM.PL", 3.0, 10.0, "800000001", null),
            ["Total"],
        ]));

        Assert.Equal(
            [
                new XtbClosedPosition("2700000000", "NEW.PL", "Newco SA", "IKE", 7m, 0.002m, Opened, "OLD.PL spinoff", "BUY", "STOCK"),
                new XtbClosedPosition("800000001", "ACM.PL", "Newco SA", "IKE", 3m, 10m, Opened, null, "BUY", "STOCK")
            ],
            file.ClosedPositions);
    }

    [Fact]
    public void APositionIdWrittenAsFloatTextIsAnInteger()
    {
        var file = Parse(Cash(), ("Closed", [ClosedHeader, Closed("NEW.PL", 7.0, 0.002, "2.7E9", null)]));

        Assert.Equal("2700000000", Assert.Single(file.ClosedPositions).PositionId);
    }

    [Fact]
    public void WithoutAClosedPositionsSheetThereAreNone() =>
        Assert.Empty(Parse(Cash()).ClosedPositions);

    [Fact]
    public void OpenPositionLotsCarryTheirOpenPriceTimeProductAndInstrument()
    {
        var file = Parse(Cash(), ("Open Positions", [
            ["Product", "Instrument/Position", "Ticker", "Category", "Type", "Volume", "Value", "Current price", "Open price",
                "Open time (UTC)"],
            ["IKE", "Newco SA", "NEW.PL", "STOCK", null, 7.0, 21.0, 3.0, null, null],
            ["IKE", 2.7e9, "NEW.PL", null, "BUY", 7.0, 21.0, 3.0, 0.002, OpenTime],
        ]));

        Assert.Equal([new XtbOpenPosition("2700000000", "NEW.PL", 7m, 0.002m, Opened, "IKE", "Newco SA", "BUY", "STOCK")], file.OpenPositions);
    }
}
