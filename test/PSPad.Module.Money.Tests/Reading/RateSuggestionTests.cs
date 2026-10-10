using PSPad.Module.Money.Reading;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Reading;

[UnitTest]
public class RateSuggestionTests
{
    static Money Eur(decimal rate, int day) => new(10m, "EUR", rate, new DateOnly(2026, 10, day));

    [Fact]
    public void PlnIsAlwaysOne() =>
        Assert.Equal(new RateSuggestion(1m, null), RateSuggestion.For("PLN", []));

    [Fact]
    public void WithoutAnEntryInThatCurrencyThereIsNoSuggestion()
    {
        var budget = ABudget();
        Assert.Null(RateSuggestion.For("EUR", [AnExpense(budget), AnExpense(budget, money: new Money(1m, "USD", 3.9m, Today))]));
    }

    [Fact]
    public void TheLatestRateDateWinsWhateverTheRecordingOrder()
    {
        var budget = ABudget();
        var later = AnExpense(budget, money: Eur(4.30m, 5), at: Now);
        var earlier = AnExpense(budget, money: Eur(4.20m, 1), at: Now.AddHours(1));

        Assert.Equal(new RateSuggestion(4.30m, new DateOnly(2026, 10, 5)), RateSuggestion.For("EUR", [earlier, later]));
    }

    [Fact]
    public void ATieGoesToTheNewestEntry()
    {
        var budget = ABudget();
        var first = AnExpense(budget, money: Eur(4.20m, 5), at: Now);
        var second = AnIncome(budget, money: Eur(4.25m, 5), at: Now.AddMinutes(5));

        Assert.Equal(4.25m, RateSuggestion.For("EUR", [first, second])!.RateToPln);
    }

    [Fact]
    public void DeletedEntriesAreIgnored()
    {
        var budget = ABudget();
        var kept = AnExpense(budget, money: Eur(4.20m, 1));
        var deleted = Delete(AnExpense(budget, money: Eur(4.90m, 9)), budget);

        Assert.Equal(4.20m, RateSuggestion.For("EUR", [kept, deleted])!.RateToPln);
    }

    [Fact]
    public void TheCurrencyIsMatchedIgnoringCase()
    {
        var budget = ABudget();
        Assert.Equal(4.20m, RateSuggestion.For(" eur ", [AnExpense(budget, money: Eur(4.20m, 1))])!.RateToPln);
    }
}
