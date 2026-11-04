using PSPad.Module.Investments.Import;
using PSPad.Module.Investments.Tests.Import.Xlsx;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Investments.Tests.Import;

[UnitTest]
public class XtbPartialFillTests
{
    const double Time = 46300.5;
    const double Minute = 1.0 / 1440;

    static XtbFile ParseRows(params object?[][] rows) =>
        XtbParser.Parse(XlsxReader.Read(XlsxBuilder.Book(("Cash Operations", XtbSheets.CashOperations("12345678", rows)))));

    static object?[] Buy(object id, string comment, object position, double time = Time, string ticker = "ACM.PL") =>
        XtbSheets.Row("Stock purchase", "Acme Works SA", ticker, time, -10.0, id, comment, "My Trades", position);

    static object?[] Sell(object id, string comment, object position) =>
        XtbSheets.Row("Stock sell", "Acme Works SA", "ACM.PL", Time + 1, 10.0, id, comment, "My Trades", position);

    [Fact]
    public void TheFillsOfOneOrderBecomeOnePositionUnderTheSmallestId()
    {
        var file = ParseRows(
            Buy("3", "OPEN BUY 4/7 @ 10.10", "800000012", Time + Minute),
            Buy("2", "OPEN BUY 3/7 @ 10.00", "800000011"));

        Assert.All(file.Rows, row => Assert.Equal("800000011", row.PositionId));
    }

    [Fact]
    public void ASellOfAnyFillClosesTheMergedPosition()
    {
        var file = ParseRows(
            Sell("4", "CLOSE BUY 2/7 @ 12.00", "800000012"),
            Buy("3", "OPEN BUY 4/7 @ 10.10", "800000012"),
            Buy("2", "OPEN BUY 3/7 @ 10.00", "800000011"));

        Assert.Equal("800000011", file.Rows[0].PositionId);
    }

    [Fact]
    public void FillsOfDifferentOrdersStayApart()
    {
        var file = ParseRows(
            Buy("1", "OPEN BUY 3/7 @ 10.00", "800000011"),
            Buy("2", "OPEN BUY 4/7 @ 10.00", "800000012", Time + Minute),
            Buy("3", "OPEN BUY 3/7 @ 10.00", "800000013", Time + 2 * Minute),
            Buy("4", "OPEN BUY 4/9 @ 10.00", "800000014", Time + 3 * Minute),
            Buy("5", "OPEN BUY 4/7 @ 10.00", "800000015", Time + 30 * Minute),
            Buy("6", "OPEN BUY 1/7 @ 10.00", "800000016", Time + Minute, "BCD.PL"));

        Assert.Equal(["800000011", "800000011", "800000013", "800000014", "800000015", "800000016"],
            file.Rows.Select(row => row.PositionId));
    }

    [Fact]
    public void PlainBuysKeepTheirPositions()
    {
        var file = ParseRows(
            Buy("1", "OPEN BUY 3 @ 10.00", "800000011"),
            Buy("2", "OPEN BUY 4 @ 10.00", "800000012"));

        Assert.Equal(["800000011", "800000012"], file.Rows.Select(row => row.PositionId));
    }
}
