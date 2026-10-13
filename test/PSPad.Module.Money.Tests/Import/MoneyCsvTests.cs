using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Import;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Money.Tests.Import;

[UnitTest]
public class MoneyCsvTests
{
    const string EntriesHeader = "kind,date,name,category,amount,currency,rate,note\n";

    [Fact]
    public void AnEntriesFileYieldsRows()
    {
        var parse = MoneyCsv.ParseEntries(EntriesHeader +
            "expense,2026-09-03,Biedronka,Food,84.20,PLN,,\n" +
            "Income,2026-09-10, Salary ,Salary,9500.00,,,September\n" +
            "expense,2026-09-05,Steam,PC,19.99,eur,4.2731,\n");

        Assert.Null(parse.FileError);
        Assert.Empty(parse.Errors);
        Assert.Equal(3, parse.Rows.Count);
        var income = parse.Rows[1];
        Assert.Equal(new ParsedEntryRow(3, CategoryKind.Income, new DateOnly(2026, 9, 10), "Salary", "Salary", 9500m,
            null, null, "September"), income);
        Assert.Equal("EUR", parse.Rows[2].Currency);
        Assert.Equal(4.2731m, parse.Rows[2].Rate);
    }

    [Fact]
    public void ColumnsMayComeInAnyOrderAndCaseWithExtrasIgnored()
    {
        var parse = MoneyCsv.ParseEntries("Amount;KIND;Mine;date;Name;Category\n12,5;expense;x;03.09.2026;Bread;Food\n");

        var row = Assert.Single(parse.Rows);
        Assert.Equal(12.5m, row.Amount);
        Assert.Null(row.Currency);
    }

    [Fact]
    public void ARateOnAPlnRowIsDropped()
    {
        var row = Assert.Single(MoneyCsv.ParseEntries(EntriesHeader + "expense,2026-09-03,A,Food,1,PLN,4.2,\n").Rows);

        Assert.Null(row.Rate);
    }

    [Theory]
    [InlineData("refund,2026-09-03,A,Food,1,,,", "Kind must be expense or income.")]
    [InlineData("expense,2026-09-03,,Food,1,,,", "An entry needs a name.")]
    [InlineData("expense,2026-09-03,A,,1,,,", "A category needs a name.")]
    [InlineData("expense,2026-09-03,A,Food,0,,,", "An amount has to be more than zero.")]
    [InlineData("expense,2026-09-03,A,Food,1,XYZ,,", "XYZ is not a currency PSPad knows.")]
    [InlineData("expense,2026-09-03,A,Food,1,EUR,0,", "A rate to PLN has to be more than zero.")]
    public void ABrokenRowBecomesAnErrorWithTheCommandsMessage(string row, string message)
    {
        var parse = MoneyCsv.ParseEntries(EntriesHeader + row + "\n");

        Assert.Empty(parse.Rows);
        Assert.Equal(new CsvRowError(2, message), Assert.Single(parse.Errors));
    }

    [Fact]
    public void AMissingRequiredColumnIsAFileError()
    {
        Assert.Equal("The file has no category column.",
            MoneyCsv.ParseEntries("kind,date,name,amount\nexpense,2026-09-03,A,1\n").FileError);
    }

    [Fact]
    public void ARepeatedColumnIsAFileError()
    {
        Assert.Equal("The column name appears twice.",
            MoneyCsv.ParseEntries("kind,date,name,Name,category,amount\n").FileError);
    }

    [Fact]
    public void MoreThanTheRowCapIsAFileError()
    {
        var rows = string.Concat(Enumerable.Repeat("expense,2026-09-03,A,Food,1\n", MoneyCsv.MaxRows + 1));

        Assert.Equal("A file can hold at most 5000 rows. Split it and import each part.",
            MoneyCsv.ParseEntries("kind,date,name,category,amount\n" + rows).FileError);
    }

    [Fact]
    public void ABalancesFileYieldsRows()
    {
        var parse = MoneyCsv.ParseBalances("month;holding;type;amount;currency;rate\n" +
            "2026-09;Wallet;cash;350,00;;\n" +
            "09.2026;ETF;Investment;2500;EUR;4,2650\n" +
            "2026-09;Box;gold;1;;\n");

        Assert.Equal(new ParsedHoldingRow(2, new YearMonth(2026, 9), "Wallet", HoldingType.Cash, 350m, null, null),
            parse.Rows[0]);
        Assert.Equal(HoldingType.Investment, parse.Rows[1].Type);
        Assert.Equal(4.2650m, parse.Rows[1].Rate);
        Assert.Equal(new CsvRowError(4, "Type must be cash, savings or investment."), Assert.Single(parse.Errors));
    }
}
