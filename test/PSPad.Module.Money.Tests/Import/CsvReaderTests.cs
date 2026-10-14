using PSPad.Module.Money.Import;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Money.Tests.Import;

[UnitTest]
public class CsvReaderTests
{
    [Fact]
    public void ItSplitsACommaFileIntoHeaderAndLines()
    {
        var table = CsvReader.Read("kind,date\nexpense,2026-09-03\nincome,2026-09-10\n");

        Assert.Null(table.Error);
        Assert.Equal(["kind", "date"], table.Header);
        Assert.Equal(2, table.Lines.Count);
        Assert.Equal(2, table.Lines[0].Number);
        Assert.Equal(["income", "2026-09-10"], table.Lines[1].Fields);
    }

    [Fact]
    public void ItDetectsASemicolonFile()
    {
        var table = CsvReader.Read("kind;amount\r\nexpense;84,20\r\n");

        Assert.Equal(["expense", "84,20"], Assert.Single(table.Lines).Fields);
    }

    [Fact]
    public void ItDropsAByteOrderMark()
    {
        var table = CsvReader.Read("\uFEFFkind,date\nexpense,2026-09-03");

        Assert.Equal("kind", table.Header[0]);
    }

    [Fact]
    public void QuotedFieldsHoldTheDelimiterQuotesAndLineBreaks()
    {
        var table = CsvReader.Read("name,note\n\"Smith, J.\",\"said \"\"hi\"\"\nthen left\"\nnext,row\n");

        Assert.Equal(["Smith, J.", "said \"hi\"\nthen left"], table.Lines[0].Fields);
        Assert.Equal(4, table.Lines[1].Number);
    }

    [Fact]
    public void BlankLinesAreSkippedButCounted()
    {
        var table = CsvReader.Read("\nkind,date\n\n , \nexpense,2026-09-03\n");

        Assert.Equal(2, table.Header.Count);
        Assert.Equal(5, Assert.Single(table.Lines).Number);
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("kind,date;amount")]
    [InlineData("")]
    public void AHeaderWithoutExactlyOneDelimiterIsAFileError(string text)
    {
        Assert.Equal("The header row must separate columns with , or ;.", CsvReader.Read(text).Error);
    }

    [Fact]
    public void AnUnclosedQuoteIsAFileError()
    {
        Assert.Equal("Line 2: a quoted field is never closed.", CsvReader.Read("a,b\n\"open,x\n").Error);
    }
}
