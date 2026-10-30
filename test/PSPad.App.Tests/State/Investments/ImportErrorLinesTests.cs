using PSPad.App.Resources;
using PSPad.App.State.Investments;
using PSPad.App.Tests.Localization;
using PSPad.Module.Investments.Import;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class ImportErrorLinesTests
{
    static readonly Microsoft.Extensions.Localization.IStringLocalizer<InvestmentImportStrings> T =
        TestLocalizers.For<InvestmentImportStrings>();

    static XtbRow Row(int sheetRow, string type = "Stock purchase", string? ticker = "ABC.US", string? id = "1001") =>
        new(sheetRow, type, XtbRowKind.Buy, "Abc Corp", ticker, new DateTimeOffset(2026, 3, 4, 9, 30, 0, TimeSpan.Zero),
            -12.5m, id, "OPEN BUY 1", "IKE", "77", null, null, []);

    static ImportRowPreview Wrong(XtbRow row, params string[] reasons) =>
        new(row, ImportRowStatus.Error, null, null, reasons, false);

    static ImportRowPreview Clean(XtbRow row) =>
        new(row, ImportRowStatus.ToImport, null, new DateOnly(2026, 3, 4), [], true);

    [Fact]
    public void AWrongRowShowsItsSheetRowTheValuesXtbGaveAndTheReasons()
    {
        var lines = ImportErrorLines.Of([Wrong(Row(87), "The comment does not say how many shares.", "Second reason.")], T);

        var line = Assert.Single(lines);
        Assert.Equal("Row 87", line.RowLabel);
        Assert.Equal("Stock purchase", line.Type);
        Assert.Equal("2026-03-04 09:30", line.Time);
        Assert.Equal("ABC.US", line.Ticker);
        Assert.Equal("1001", line.Id);
        Assert.Equal("OPEN BUY 1", line.Comment);
        Assert.Equal(["The comment does not say how many shares.", "Second reason."], line.Reasons);
    }

    [Fact]
    public void OnlyWrongRowsAreListedInSheetOrder()
    {
        var rows = new[] { Clean(Row(5)), Wrong(Row(41), "a"), Clean(Row(6)), Wrong(Row(87), "b") };

        Assert.Equal(["Row 41", "Row 87"], ImportErrorLines.Of(rows, T).Select(line => line.RowLabel));
    }

    [Fact]
    public void ErrorsOnlyHidesCleanRowsAndOtherwiseEveryRowShows()
    {
        var rows = new[] { Clean(Row(5)), Wrong(Row(41), "a") };

        Assert.Equal([41], ImportRowFilter.Visible(rows, true).Select(row => row.Row.SheetRow));
        Assert.Equal([5, 41], ImportRowFilter.Visible(rows, false).Select(row => row.Row.SheetRow));
    }
}
