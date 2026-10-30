using PSPad.TestInfrastructure;
using PSPad.Module.Investments.Import;

namespace PSPad.Module.Investments.Tests.Import.Xlsx;

[UnitTest]
public class XlsxReaderTests
{
    [Fact]
    public void SharedInlineFormulaStringsAndNumbersAreRead()
    {
        var book = XlsxReader.Read(XlsxBuilder.Book(("S", [["shared", new InlineText("inline"), new FormulaText("formula"), 12.5]])));

        var row = Assert.Single(Assert.Single(book.Sheets).Rows);
        Assert.Equal("shared", row[0]!.Text);
        Assert.Equal("inline", row[1]!.Text);
        Assert.Equal("formula", row[2]!.Text);
        Assert.Equal(12.5, row[3]!.Number);
        Assert.Null(row[3]!.Text);
    }

    [Fact]
    public void SkippedCellsKeepTheirColumns()
    {
        var book = XlsxReader.Read(XlsxBuilder.Book(("S", [["a", null, null, "d"]])));

        var row = Assert.Single(book.Sheets[0].Rows);
        Assert.Equal("a", row[0]!.Text);
        Assert.Null(row[1]);
        Assert.Null(row[2]);
        Assert.Equal("d", row[3]!.Text);
        Assert.Equal("D1", row[3]!.Reference);
    }

    [Fact]
    public void SkippedRowsKeepTheirIndexes()
    {
        var book = XlsxReader.Read(XlsxBuilder.Book(("S", [["first"], [], ["third"]])));

        var rows = book.Sheets[0].Rows;
        Assert.Equal(3, rows.Count);
        Assert.Empty(rows[1]);
        Assert.Equal("third", rows[2][0]!.Text);
    }

    [Fact]
    public void SheetsKeepTheirNamesAndOrder()
    {
        var book = XlsxReader.Read(XlsxBuilder.Book(("One", [["a"]]), ("Dwa", [["b"]])));

        Assert.Equal(["One", "Dwa"], book.Sheets.Select(s => s.Name));
    }

    [Fact]
    public void ANonZipFileIsAFileError()
    {
        var error = Assert.Throws<XtbFileError>(() => XlsxReader.Read(XlsxBuilder.Bytes("not a zip at all"u8.ToArray())));

        Assert.Equal("This file is not an Excel workbook.", error.Message);
    }

    [Fact]
    public void AFileOverFiveMegabytesIsAFileError()
    {
        var error = Assert.Throws<XtbFileError>(() => XlsxReader.Read(XlsxBuilder.Bytes(new byte[XlsxReader.MaxBytes + 1])));

        Assert.Equal("This file is larger than 5 MB.", error.Message);
    }

    [Fact]
    public void APartLargerThanThePartCapIsAFileError()
    {
        var padding = new string(' ', (int)XlsxReader.MaxPartBytes + 1);

        var error = Assert.Throws<XtbFileError>(() => XlsxReader.Read(XlsxBuilder.RawSheets(XlsxBuilder.SheetWith("", padding))));

        Assert.Equal("This file is not an Excel workbook.", error.Message);
    }

    [Fact]
    public void PartsThatTogetherExceedTheTotalBudgetAreAFileError()
    {
        var padding = new string(' ', (int)(XlsxReader.MaxPartBytes * 3 / 4));
        var sheets = Enumerable.Repeat(XlsxBuilder.SheetWith("", padding), 3).ToArray();

        Assert.True(sheets.Length * padding.Length > XlsxReader.MaxTotalBytes);
        var error = Assert.Throws<XtbFileError>(() => XlsxReader.Read(XlsxBuilder.RawSheets(sheets)));

        Assert.Equal("This file is not an Excel workbook.", error.Message);
    }

    [Fact]
    public void ASheetWithADoctypeIsAFileError()
    {
        var sheet = "<?xml version=\"1.0\"?><!DOCTYPE worksheet [<!ENTITY x \"y\">]><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData/></worksheet>";

        var error = Assert.Throws<XtbFileError>(() => XlsxReader.Read(XlsxBuilder.RawSheets(sheet)));

        Assert.Equal("This file is not an Excel workbook.", error.Message);
    }

    [Fact]
    public void ARowFarBeyondTheLimitIsRejectedWithoutPadding()
    {
        var sheet = XlsxBuilder.SheetWith("<row r=\"1000000\"><c r=\"A1000000\"><v>1</v></c></row>");

        var error = Assert.Throws<XtbFileError>(() => XlsxReader.Read(XlsxBuilder.RawSheets(sheet)));

        Assert.Equal("This file has more than 20,000 rows. Export a shorter period.", error.Message);
    }

    [Fact]
    public void ACellFarToTheRightIsIgnoredWithoutPadding()
    {
        var sheet = XlsxBuilder.SheetWith("<row r=\"1\"><c r=\"A1\"><v>1</v></c><c r=\"XFD1\"><v>2</v></c></row>");

        var row = Assert.Single(Assert.Single(XlsxReader.Read(XlsxBuilder.RawSheets(sheet)).Sheets).Rows);

        Assert.Single(row);
        Assert.Equal(1, row[0]!.Number);
    }
}
