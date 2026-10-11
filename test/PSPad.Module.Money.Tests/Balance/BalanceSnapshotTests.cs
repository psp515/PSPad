using System.Text.Json;
using PSPad.Abstractions;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Balance;

[UnitTest]
public class BalanceSnapshotTests
{
    static readonly YearMonth August = new(2026, 8);
    static readonly YearMonth September = new(2026, 9);
    static readonly DateOnly OctoberLastDay = new(2026, 10, 31);

    static Money Pln(decimal amount) => new(amount, "PLN", 1m, Today);

    static StartBalanceMonth Start(Budget budget, YearMonth month, Guid? actor = null) =>
        new(Guid.NewGuid(), actor ?? Owner, budget.Id, month);

    static AddHolding Add(Budget budget, YearMonth month, string name = "Wallet", HoldingType type = HoldingType.Cash,
        Money? money = null, Guid? holdingId = null, Guid? actor = null) =>
        new(Guid.NewGuid(), actor ?? Owner, budget.Id, month, holdingId ?? Guid.NewGuid(), name, type, money ?? Pln(100m));

    static EditHolding Edit(Budget budget, YearMonth month, Holding holding, string? name = null,
        HoldingType? type = null, Money? money = null) =>
        new(Guid.NewGuid(), Owner, budget.Id, month, holding.HoldingId, name ?? holding.Name, type ?? holding.Type,
            money ?? holding.Money);

    static string Rejection(BalanceSnapshot? snapshot, Budget? budget, ICommand command) =>
        Assert.Throws<DomainRejectedException>(() => BalanceSnapshot.Decide(snapshot, budget, command, Now)).Message;

    [Fact]
    public void IdForIsStablePerBudgetAndMonth()
    {
        var budgetId = Guid.NewGuid();

        Assert.Equal(BalanceSnapshot.IdFor(budgetId, October), BalanceSnapshot.IdFor(budgetId, October));
        Assert.NotEqual(BalanceSnapshot.IdFor(budgetId, October), BalanceSnapshot.IdFor(budgetId, November));
        Assert.NotEqual(BalanceSnapshot.IdFor(budgetId, October), BalanceSnapshot.IdFor(budgetId, new YearMonth(2027, 10)));
        Assert.NotEqual(BalanceSnapshot.IdFor(budgetId, October), BalanceSnapshot.IdFor(Guid.NewGuid(), October));
        Assert.NotEqual(budgetId, BalanceSnapshot.IdFor(budgetId, October));
    }

    [Fact]
    public void StartingTheFirstMonthStartsEmpty()
    {
        var budget = ABudget();
        var snapshot = new BalanceSnapshot();

        snapshot.ApplyAll(BalanceSnapshot.Decide(null, budget, [], Start(budget, October), Now));

        Assert.Equal(BalanceSnapshot.IdFor(budget.Id, October), snapshot.Id);
        Assert.Equal(budget.UserId, snapshot.UserId);
        Assert.Equal(budget.Id, snapshot.BudgetId);
        Assert.Equal(October, snapshot.Month);
        Assert.Empty(snapshot.Holdings);
        Assert.Equal(Now, snapshot.UpdatedAt);
        Assert.False(snapshot.Deleted);
    }

    [Fact]
    public void StartingCarriesTheLatestEarlierLiveBalanceForward()
    {
        var budget = ABudget();
        var wallet = AHolding("Wallet", 100m);
        var konto = AHolding("Konto", 10m, "EUR", 4.25m, new DateOnly(2026, 9, 29), HoldingType.Savings);
        var august = ASnapshot(budget, August, [AHolding("Old", 1m)]);
        var september = ASnapshot(budget, September, [wallet, konto]);
        var december = ASnapshot(budget, new YearMonth(2026, 12), [AHolding("Later", 1m)]);
        var otherBudget = ABudget();
        var foreign = ASnapshot(otherBudget, September, [AHolding("Theirs", 1m)]);

        var started = Assert.IsType<BalanceMonthStarted>(Assert.Single(
            BalanceSnapshot.Decide(null, budget, [august, september, december, foreign], Start(budget, October), Now)));

        Holding[] expected =
        [
            wallet with { Money = new Money(100m, "PLN", 1m, new DateOnly(2026, 9, 30)) },
            konto with { Money = new Money(10m, "EUR", 4.25m, new DateOnly(2026, 9, 29)) }
        ];
        Assert.Equal(expected, started.Holdings);
        Assert.Equal(October, started.Month);
        Assert.Equal(budget.Id, started.BudgetId);
    }

    [Fact]
    public void ADeletedEarlierMonthIsSkipped()
    {
        var budget = ABudget();
        var august = ASnapshot(budget, August, [AHolding("Kept", 5m)]);
        var september = DeleteSnapshot(ASnapshot(budget, September, [AHolding("Gone", 9m)]), budget);

        var started = Assert.IsType<BalanceMonthStarted>(Assert.Single(
            BalanceSnapshot.Decide(null, budget, [august, september], Start(budget, October), Now)));

        Assert.Equal("Kept", Assert.Single(started.Holdings).Name);
    }

    [Fact]
    public void StartingEarlierThanAnExistingMonthCopiesWhatPrecedesIt()
    {
        var budget = ABudget();
        var august = ASnapshot(budget, August, [AHolding("August", 5m)]);
        var october = ASnapshot(budget, October, [AHolding("October", 7m)]);

        var started = Assert.IsType<BalanceMonthStarted>(Assert.Single(
            BalanceSnapshot.Decide(null, budget, [august, october], Start(budget, September), Now)));

        Assert.Equal("August", Assert.Single(started.Holdings).Name);
        Assert.Equal(new DateOnly(2026, 8, 31), started.Holdings[0].Money.RateDate);
    }

    [Fact]
    public void StartingAMonthThatHasABalanceChangesNothing()
    {
        var budget = ABudget();
        var october = ASnapshot(budget, October, [AHolding()]);

        Assert.Empty(BalanceSnapshot.Decide(october, budget, [october], Start(budget, October), Now));
    }

    [Fact]
    public void StartingADeletedMonthAgainRevivesItWithFreshHoldings()
    {
        var budget = ABudget();
        var september = ASnapshot(budget, September, [AHolding("Fresh", 3m)]);
        var october = DeleteSnapshot(ASnapshot(budget, October, [AHolding("Stale", 8m)]), budget);

        october.ApplyAll(BalanceSnapshot.Decide(october, budget, [september, october], Start(budget, October), Now));

        Assert.False(october.Deleted);
        Assert.Equal(BalanceSnapshot.IdFor(budget.Id, October), october.Id);
        Assert.Equal("Fresh", Assert.Single(october.Holdings).Name);
    }

    [Fact]
    public void AddingAHoldingFoldsIt()
    {
        var budget = ABudget();
        var snapshot = ASnapshot(budget, October);
        var holdingId = Guid.NewGuid();

        snapshot.ApplyAll(BalanceSnapshot.Decide(snapshot, budget,
            Add(budget, October, "  Konto ", HoldingType.Savings, new Money(10m, "eur", 4.25m, new DateOnly(2026, 10, 30)),
                holdingId), Now.AddHours(1)));

        Assert.Equal(
            new Holding(holdingId, "Konto", HoldingType.Savings, new Money(10m, "EUR", 4.25m, new DateOnly(2026, 10, 30))),
            Assert.Single(snapshot.Holdings));
        Assert.Equal(Now.AddHours(1), snapshot.UpdatedAt);
    }

    [Fact]
    public void APlnHoldingIsDatedTheMonthsLastDay()
    {
        var budget = ABudget();
        var snapshot = ASnapshot(budget, October);

        snapshot.ApplyAll(BalanceSnapshot.Decide(snapshot, budget, Add(budget, October, money: new Money(50m, "PLN", 3m, Today)), Now));

        Assert.Equal(new Money(50m, "PLN", 1m, OctoberLastDay), Assert.Single(snapshot.Holdings).Money);
    }

    [Fact]
    public void AddingTheSameHoldingTwiceIsRejected()
    {
        var budget = ABudget();
        var wallet = AHolding();
        var snapshot = ASnapshot(budget, October, [wallet]);

        Assert.Equal("That holding already exists.",
            Rejection(snapshot, budget, Add(budget, October, holdingId: wallet.HoldingId)));
    }

    [Theory]
    [InlineData("", "A holding needs a name.")]
    [InlineData("   ", "A holding needs a name.")]
    public void AHoldingNeedsAName(string name, string message)
    {
        var budget = ABudget();
        var snapshot = ASnapshot(budget, October);

        Assert.Equal(message, Rejection(snapshot, budget, Add(budget, October, name)));
    }

    [Fact]
    public void AHoldingNameIsAtMostEightyCharacters()
    {
        var budget = ABudget();
        var snapshot = ASnapshot(budget, October);

        Assert.Equal("A holding name is at most 80 characters.",
            Rejection(snapshot, budget, Add(budget, October, new string('h', 81))));
        Assert.Single(BalanceSnapshot.Decide(snapshot, budget, Add(budget, October, new string('h', 80)), Now));
    }

    [Fact]
    public void AnUnknownHoldingTypeIsRejected()
    {
        var budget = ABudget();
        var snapshot = ASnapshot(budget, October);

        Assert.Equal("That holding type does not exist.",
            Rejection(snapshot, budget, Add(budget, October, type: (HoldingType)7)));
    }

    [Fact]
    public void AHoldingsMoneyFollowsTheMoneyRules()
    {
        var budget = ABudget();
        var snapshot = ASnapshot(budget, October);

        Assert.Equal("An amount has to be more than zero.",
            Rejection(snapshot, budget, Add(budget, October, money: Pln(0m))));
        Assert.Equal("An amount can be at most 1 000 000 000.",
            Rejection(snapshot, budget, Add(budget, October, money: Pln(1_000_000_001m))));
        Assert.Equal("A rate can be at most 10 000.",
            Rejection(snapshot, budget, Add(budget, October, money: new Money(1m, "EUR", 10_001m, Today))));
        Assert.Equal("XYZ is not a currency PSPad knows.",
            Rejection(snapshot, budget, Add(budget, October, money: new Money(1m, "XYZ", 1m, Today))));
    }

    [Fact]
    public void EditingReplacesTheHolding()
    {
        var budget = ABudget();
        var wallet = AHolding();
        var snapshot = ASnapshot(budget, October, [wallet, AHolding("Konto")]);

        snapshot.ApplyAll(BalanceSnapshot.Decide(snapshot, budget,
            Edit(budget, October, wallet, "Cash box", HoldingType.Investment, Pln(70m)), Now));

        Assert.Equal(new Holding(wallet.HoldingId, "Cash box", HoldingType.Investment, new Money(70m, "PLN", 1m, OctoberLastDay)),
            snapshot.Holdings[0]);
        Assert.Equal("Konto", snapshot.Holdings[1].Name);
    }

    [Fact]
    public void EditingWithoutAChangeEmitsNothing()
    {
        var budget = ABudget();
        var wallet = AHolding();
        var snapshot = ASnapshot(budget, October, [wallet]);

        Assert.Empty(BalanceSnapshot.Decide(snapshot, budget, Edit(budget, October, snapshot.Holdings[0]), Now));
    }

    [Fact]
    public void EditingOrRemovingAMissingHoldingIsRejected()
    {
        var budget = ABudget();
        var snapshot = ASnapshot(budget, October, [AHolding()]);
        var stranger = AHolding("Elsewhere");

        Assert.Equal("That holding does not exist.", Rejection(snapshot, budget, Edit(budget, October, stranger)));
        Assert.Equal("That holding does not exist.", Rejection(snapshot, budget,
            new RemoveHolding(Guid.NewGuid(), Owner, budget.Id, October, stranger.HoldingId)));
    }

    [Fact]
    public void RemovingDropsTheHolding()
    {
        var budget = ABudget();
        var wallet = AHolding();
        var snapshot = ASnapshot(budget, October, [wallet, AHolding("Konto")]);

        snapshot.ApplyAll(BalanceSnapshot.Decide(snapshot, budget,
            new RemoveHolding(Guid.NewGuid(), Owner, budget.Id, October, wallet.HoldingId), Now));

        Assert.Equal("Konto", Assert.Single(snapshot.Holdings).Name);
    }

    [Fact]
    public void HoldingCommandsNeedALiveMonth()
    {
        var budget = ABudget();
        var wallet = AHolding();
        var deleted = DeleteSnapshot(ASnapshot(budget, October, [wallet]), budget);

        foreach (var snapshot in new BalanceSnapshot?[] { null, deleted })
        {
            Assert.Equal("That month has no balance yet.", Rejection(snapshot, budget, Add(budget, October)));
            Assert.Equal("That month has no balance yet.", Rejection(snapshot, budget, Edit(budget, October, wallet)));
            Assert.Equal("That month has no balance yet.", Rejection(snapshot, budget,
                new RemoveHolding(Guid.NewGuid(), Owner, budget.Id, October, wallet.HoldingId)));
        }
    }

    [Fact]
    public void AnotherBudgetsSnapshotIsNotThisMonthsBalance()
    {
        var budget = ABudget();
        var other = ABudget();
        var theirs = ASnapshot(other, October);

        Assert.Equal("That month has no balance yet.", Rejection(theirs, budget, Add(budget, October)));
    }

    [Fact]
    public void StartingAMonthOverAnotherBudgetsSnapshotIsRejected()
    {
        var budget = ABudget();
        var other = ABudget();

        Assert.Equal("That budget does not exist.",
            Rejection(ASnapshot(other, October), budget, Start(budget, October)));
        Assert.Equal("That budget does not exist.",
            Rejection(DeleteSnapshot(ASnapshot(other, October), other), budget, Start(budget, October)));
    }

    [Fact]
    public void DeletingAnotherBudgetsSnapshotIsRejected()
    {
        var budget = ABudget();
        var other = ABudget();
        var delete = new DeleteBalanceMonth(Guid.NewGuid(), Owner, budget.Id, October);

        Assert.Equal("That month has no balance yet.", Rejection(ASnapshot(other, October), budget, delete));
        Assert.Equal("That month has no balance yet.",
            Rejection(DeleteSnapshot(ASnapshot(other, October), other), budget, delete));
    }

    [Fact]
    public void DeletingSoftDeletesAndIsANoOpTheSecondTime()
    {
        var budget = ABudget();
        var snapshot = ASnapshot(budget, October, [AHolding()]);
        var delete = new DeleteBalanceMonth(Guid.NewGuid(), Owner, budget.Id, October);

        snapshot.ApplyAll(BalanceSnapshot.Decide(snapshot, budget, delete, Now));

        Assert.True(snapshot.Deleted);
        Assert.Empty(BalanceSnapshot.Decide(snapshot, budget, delete, Now));
    }

    [Fact]
    public void DeletingAMonthWithoutABalanceIsRejected()
    {
        var budget = ABudget();

        Assert.Equal("That month has no balance yet.",
            Rejection(null, budget, new DeleteBalanceMonth(Guid.NewGuid(), Owner, budget.Id, October)));
    }

    [Fact]
    public void EveryWriteToAnArchivedBudgetIsRejected()
    {
        var budget = ABudget();
        var wallet = AHolding();
        var snapshot = ASnapshot(budget, October, [wallet]);
        Archive(budget);

        Assert.Equal("This budget is archived.", Rejection(null, budget, Start(budget, November)));
        Assert.Equal("This budget is archived.", Rejection(snapshot, budget, Add(budget, October)));
        Assert.Equal("This budget is archived.", Rejection(snapshot, budget, Edit(budget, October, wallet, "Cash")));
        Assert.Equal("This budget is archived.", Rejection(snapshot, budget,
            new RemoveHolding(Guid.NewGuid(), Owner, budget.Id, October, wallet.HoldingId)));
        Assert.Equal("This budget is archived.", Rejection(snapshot, budget,
            new DeleteBalanceMonth(Guid.NewGuid(), Owner, budget.Id, October)));
    }

    [Fact]
    public void AStrangerOrAMissingBudgetReadsAsAMissingBudget()
    {
        var budget = ABudget();
        var wallet = AHolding();
        var snapshot = ASnapshot(budget, October, [wallet]);

        Assert.Equal("That budget does not exist.", Rejection(null, budget, Start(budget, November, Stranger)));
        Assert.Equal("That budget does not exist.", Rejection(snapshot, budget, Add(budget, October, actor: Stranger)));
        Assert.Equal("That budget does not exist.", Rejection(snapshot, budget,
            new DeleteBalanceMonth(Guid.NewGuid(), Stranger, budget.Id, October)));
        Assert.Equal("That budget does not exist.", Rejection(snapshot, null, Add(budget, October)));
        Assert.Equal("That budget does not exist.", Rejection(null, null, Start(budget, October)));
    }

    [Fact]
    public void AnUnknownCommandIsRejected()
    {
        var budget = ABudget();

        Assert.Equal("A balance snapshot cannot handle RenameBudget.",
            Rejection(null, budget, new RenameBudget(Guid.NewGuid(), Owner, budget.Id, "x")));
    }

    [Fact]
    public void ASnapshotRoundTripsThroughTheReplicaJson()
    {
        var budget = ABudget();
        var snapshot = ASnapshot(budget, October, [AHolding("Konto", 10m, "EUR", 4.25m, new DateOnly(2026, 10, 30))]);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { IncludeFields = true };

        var copy = JsonSerializer.Deserialize<BalanceSnapshot>(JsonSerializer.Serialize(snapshot, options), options)!;

        Assert.Equal(snapshot.Id, copy.Id);
        Assert.Equal(budget.Id, copy.BudgetId);
        Assert.Equal(October, copy.Month);
        Assert.Equal(snapshot.Holdings, copy.Holdings);
        Assert.Equal(snapshot.UpdatedAt, copy.UpdatedAt);
    }
}
