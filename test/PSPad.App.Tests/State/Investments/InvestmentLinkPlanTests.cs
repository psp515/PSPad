using PSPad.Abstractions;
using PSPad.App.State.Investments;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Reading;
using PSPad.Module.Money;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class InvestmentLinkPlanTests
{
    static readonly Guid User = Guid.NewGuid();
    static readonly Guid Budget = Guid.NewGuid();
    static readonly YearMonth Month = new(2026, 10);
    static readonly DateOnly Day = new(2026, 10, 9);

    static InvestmentAccount AnAccount(string name, string currency)
    {
        var account = new InvestmentAccount();
        account.ApplyAll(InvestmentAccount.Decide(null,
            new CreateInvestmentAccount(Guid.NewGuid(), User, Guid.NewGuid(), name, currency, Broker.Xtb, null),
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)));
        return account;
    }

    static LinkChoice Choice(InvestmentAccount account, decimal value, decimal rate = 1m, Holding? existing = null) =>
        new(account, new AccountValueOnDay(account.Id, value, account.Currency, new Dictionary<string, DateOnly?>(), false, false),
            rate, Day, existing);

    static IReadOnlyList<ICommand> Plan(bool monthExists, params LinkChoice[] ticked) =>
        InvestmentLinkPlan.Commands(User, Budget, Month, monthExists, ticked, Guid.NewGuid);

    [Fact]
    public void AnAccountBecomesAnInvestmentHoldingNamedAfterItAndLinkedToIt()
    {
        var account = AnAccount("XTB USD", "USD");

        var add = Assert.IsType<AddHolding>(Assert.Single(Plan(true, Choice(account, 100m, 3.6m))));

        Assert.Equal("XTB USD", add.Name);
        Assert.Equal(HoldingType.Investment, add.Type);
        Assert.Equal(account.Id, add.InvestmentAccountId);
        Assert.Equal(new Money(100m, "USD", 3.6m, Day), add.Money);
    }

    [Fact]
    public void AnAccountAlreadyInTheMonthIsEditedKeepingItsHolding()
    {
        var account = AnAccount("XTB IKE", "PLN");
        var holding = new Holding(Guid.NewGuid(), "My IKE", HoldingType.Savings, new Money(1m, "PLN", 1m, Day), account.Id);

        var edit = Assert.IsType<EditHolding>(Assert.Single(Plan(true, Choice(account, 50m, existing: holding))));

        Assert.Equal(holding.HoldingId, edit.HoldingId);
        Assert.Equal("My IKE", edit.Name);
        Assert.Equal(HoldingType.Savings, edit.Type);
        Assert.Equal(account.Id, edit.InvestmentAccountId);
        Assert.Equal(50m, edit.Money.Amount);
    }

    [Fact]
    public void AMonthStartedFromLastMonthEditsTheCarriedLinkAndAddsTheRest()
    {
        var linked = AnAccount("Linked", "PLN");
        var fresh = AnAccount("Fresh", "PLN");
        var carried = new Holding(Guid.NewGuid(), "Linked", HoldingType.Investment, new Money(5m, "PLN", 1m, Day), linked.Id);

        var commands = Plan(false, Choice(linked, 10m, existing: carried), Choice(fresh, 20m));

        Assert.IsType<StartBalanceMonth>(commands[0]);
        Assert.Equal(carried.HoldingId, Assert.IsType<EditHolding>(commands[1]).HoldingId);
        Assert.Equal(fresh.Id, Assert.IsType<AddHolding>(commands[2]).InvestmentAccountId);
        Assert.Equal(3, commands.Count);
    }

    [Fact]
    public void AMissingMonthIsStartedFirst()
    {
        var commands = Plan(false, Choice(AnAccount("A", "PLN"), 10m));

        Assert.IsType<StartBalanceMonth>(commands[0]);
        Assert.IsType<AddHolding>(commands[1]);
    }

    [Fact]
    public void AnExistingMonthIsNotStartedAgain() =>
        Assert.DoesNotContain(Plan(true, Choice(AnAccount("A", "PLN"), 10m)), command => command is StartBalanceMonth);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AnAccountWorthNothingIsSkipped(int value) =>
        Assert.Empty(Plan(false, Choice(AnAccount("A", "PLN"), value)));

    [Fact]
    public void EveryCommandHasItsOwnId()
    {
        var commands = Plan(false, Choice(AnAccount("A", "PLN"), 10m), Choice(AnAccount("B", "PLN"), 20m));

        Assert.Equal(commands.Count, commands.Select(command => command.CommandId).Distinct().Count());
    }
}
