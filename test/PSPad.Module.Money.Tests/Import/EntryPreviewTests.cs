using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Import;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Import;

[UnitTest]
public class EntryPreviewTests
{
    const string Header = "kind,date,name,category,amount,currency,rate,note\n";
    static readonly DateOnly September3 = new(2026, 9, 3);

    static MoneyImportPreview Preview(
        string rows, Budget? budget = null, IReadOnlyList<MoneyEntry>? entries = null, string defaultCurrency = "PLN",
        RateBook? rates = null)
    {
        var target = budget ?? ABudget();
        return MoneyImportPreview.Build(MoneyCsv.ParseEntries(Header + rows), null,
            new ImportContext(target, entries ?? [], [], defaultCurrency), rates ?? RateBook.Empty, Guid.NewGuid);
    }

    [Fact]
    public void APlnRowIsReadyWithRateOneOnItsOwnDate()
    {
        var row = Assert.Single(Preview("expense,2026-09-03,Biedronka,Food,84.20,PLN,,\n").Entries);

        Assert.Equal(ImportStatus.ToImport, row.Status);
        Assert.Equal(new Money(84.20m, "PLN", 1m, September3), row.Money);
    }

    [Fact]
    public void AnEmptyCurrencyTakesTheDefaultCurrency()
    {
        var preview = Preview("expense,2026-09-03,Steam,PC,19.99,,,\n", defaultCurrency: "EUR");

        Assert.Equal(ImportStatus.WaitingForRate, Assert.Single(preview.Entries).Status);
        Assert.Equal(new RateRequest("EUR", September3), Assert.Single(preview.MissingRates));
        Assert.True(preview.IsWaitingForRates);
    }

    [Fact]
    public void AZeroRateOnAnEmptyCurrencyRowIsAnErrorWhenTheDefaultCurrencyIsForeign()
    {
        var row = Assert.Single(Preview("expense,2026-09-03,Steam,PC,19.99,,0,\n", defaultCurrency: "EUR").Entries);

        Assert.Equal(ImportStatus.Error, row.Status);
        Assert.Equal("A rate to PLN has to be more than zero.", row.Error);
    }

    [Fact]
    public void AGivenRateIsDatedOnTheEntry()
    {
        var row = Assert.Single(Preview("expense,2026-09-03,Steam,PC,19.99,EUR,4.2731,\n").Entries);

        Assert.Equal(new Money(19.99m, "EUR", 4.2731m, September3), row.Money);
        Assert.Empty(Preview("expense,2026-09-03,Steam,PC,19.99,EUR,4.2731,\n").MissingRates);
    }

    [Fact]
    public void AKnownNbpRateIsUsedWithItsEffectiveDate()
    {
        var friday = new DateOnly(2026, 9, 2);
        var rates = new RateBook(
            new Dictionary<RateRequest, ImportRate> { [new RateRequest("EUR", September3)] = new(4.25m, friday) },
            new Dictionary<RateRequest, string>());

        var row = Assert.Single(Preview("expense,2026-09-03,Steam,PC,19.99,EUR,,\n", rates: rates).Entries);

        Assert.Equal(ImportStatus.ToImport, row.Status);
        Assert.Equal(new Money(19.99m, "EUR", 4.25m, friday), row.Money);
    }

    [Fact]
    public void AFailedNbpRateMakesTheRowAnError()
    {
        var request = new RateRequest("EUR", September3);
        var rates = new RateBook(new Dictionary<RateRequest, ImportRate>(),
            new Dictionary<RateRequest, string> { [request] = "No NBP rate for EUR on 2026-09-03." });

        var preview = Preview("expense,2026-09-03,Steam,PC,19.99,EUR,,\n", rates: rates);

        var row = Assert.Single(preview.Entries);
        Assert.Equal(ImportStatus.Error, row.Status);
        Assert.Equal("No NBP rate for EUR on 2026-09-03.", row.Error);
        Assert.Equal(request, Assert.Single(preview.MissingRates));
        Assert.False(preview.IsWaitingForRates);
    }

    [Fact]
    public void ParseErrorsShowAsErrorRowsInLineOrder()
    {
        var preview = Preview("refund,2026-09-03,A,Food,1,,,\nexpense,2026-09-03,B,Food,1,,,\n");

        Assert.Equal([2, 3], preview.Entries.Select(row => row.Line));
        Assert.Equal(ImportStatus.Error, preview.Entries[0].Status);
        Assert.Null(preview.Entries[0].Row);
    }

    [Fact]
    public void ARowMatchingALiveEntryIsAlreadyInBudget()
    {
        var budget = ABudget();
        var existing = AnExpense(budget, "Food", 84.20m, September3, name: "biedronka");
        var deleted = Delete(AnExpense(budget, "Food", 5m, September3, name: "Kiosk"), budget);

        var preview = Preview(
            "expense,2026-09-03,Biedronka,Food,84.2,PLN,,\n" +
            "expense,2026-09-03,Biedronka,Food,84.2,PLN,,\n" +
            "income,2026-09-03,Biedronka,Food,84.2,PLN,,\n" +
            "expense,2026-09-03,Kiosk,Food,5,PLN,,\n",
            budget, [existing, deleted]);

        Assert.Equal(
            [ImportStatus.AlreadyInBudget, ImportStatus.AlreadyInBudget, ImportStatus.ToImport, ImportStatus.ToImport],
            preview.Entries.Select(row => row.Status));
    }

    [Fact]
    public void NewCategoriesAreListedOncePerKindKeepingTheBudgetsSpelling()
    {
        var preview = Preview(
            "expense,2026-09-03,A,food,1,,,\nexpense,2026-09-03,B,Hobby,1,,,\nexpense,2026-09-03,C,hobby,1,,,\n" +
            "income,2026-09-03,D,Hobby,1,,,\n");

        Assert.Equal(["Hobby"], preview.NewExpenseCategories);
        Assert.Equal(["Hobby"], preview.NewIncomeCategories);
    }

    [Fact]
    public void RowsPastTheCategoryCapAreErrors()
    {
        var budget = ABudget();
        var free = Budget.MaxCategoriesPerKind - budget.ExpenseCategories.Count;
        var rows = string.Concat(Enumerable.Range(0, free + 1).Select(index => $"expense,2026-09-03,A,New{index},1,,,\n"));

        var preview = Preview(rows + "expense,2026-09-03,A,New0,1,,,\n", budget);

        Assert.Equal(free, preview.NewExpenseCategories.Count);
        var over = Assert.Single(preview.Entries, row => row.Status == ImportStatus.Error);
        Assert.Equal($"A budget can have at most {Budget.MaxCategoriesPerKind} expense categories.", over.Error);
        Assert.Equal(ImportStatus.ToImport, preview.Entries[^1].Status);
    }
}
