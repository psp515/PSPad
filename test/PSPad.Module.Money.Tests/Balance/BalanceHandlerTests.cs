using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Tests.Fakes;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Balance;

[UnitTest]
public class BalanceHandlerTests
{
    static readonly YearMonth September = new(2026, 9);

    readonly FakeDocumentStore<Budget> _budgets = new();
    readonly FakeDocumentStore<BalanceSnapshot> _snapshots = new();
    readonly FakeUnitOfWork _work = new();
    readonly FixedClock _clock = new(Now);

    Budget Seeded(Budget budget)
    {
        _budgets.Seed(budget);
        return budget;
    }

    BalanceSnapshot Seeded(BalanceSnapshot snapshot)
    {
        _snapshots.Seed(snapshot);
        return snapshot;
    }

    [Fact]
    public async Task StartingAMonthCarriesTheStoredHistoryForward()
    {
        var budget = Seeded(ABudget());
        Seeded(ASnapshot(budget, September, [AHolding("Wallet", 120m)]));

        var result = await new StartBalanceMonthHandler(_budgets, _snapshots, _work, _clock).HandleAsync(
            new StartBalanceMonth(Guid.NewGuid(), Owner, budget.Id, October), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(_work.Committed);
        var (aggregate, events) = Assert.Single(_work.Staged);
        var started = Assert.IsType<BalanceSnapshot>(aggregate);
        Assert.IsType<BalanceMonthStarted>(Assert.Single(events));
        Assert.Equal(BalanceSnapshot.IdFor(budget.Id, October), started.Id);
        Assert.Equal("Wallet", Assert.Single(started.Holdings).Name);
    }

    [Fact]
    public async Task StartingADeletedMonthRevivesTheStoredDocument()
    {
        var budget = Seeded(ABudget());
        var deleted = Seeded(DeleteSnapshot(ASnapshot(budget, October, [AHolding("Old")]), budget));

        var result = await new StartBalanceMonthHandler(_budgets, _snapshots, _work, _clock).HandleAsync(
            new StartBalanceMonth(Guid.NewGuid(), Owner, budget.Id, October), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Same(deleted, Assert.Single(_work.Staged).Aggregate);
        Assert.False(deleted.Deleted);
        Assert.Empty(deleted.Holdings);
    }

    [Fact]
    public async Task AStrangersStartIsRejectedWithoutStaging()
    {
        var budget = Seeded(ABudget());

        var result = await new StartBalanceMonthHandler(_budgets, _snapshots, _work, _clock).HandleAsync(
            new StartBalanceMonth(Guid.NewGuid(), Stranger, budget.Id, October), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal("That budget does not exist.", result.Rejection);
        Assert.Empty(_work.Staged);
        Assert.False(_work.Committed);
    }

    [Fact]
    public async Task AddingAHoldingStagesTheMonth()
    {
        var budget = Seeded(ABudget());
        var snapshot = Seeded(ASnapshot(budget, October));
        var holdingId = Guid.NewGuid();

        var result = await new AddHoldingHandler(_budgets, _snapshots, _work, _clock).HandleAsync(
            new AddHolding(Guid.NewGuid(), Owner, budget.Id, October, holdingId, "Konto", HoldingType.Savings,
                new Money(10m, "EUR", 4.25m, Today)), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Same(snapshot, Assert.Single(_work.Staged).Aggregate);
        Assert.Equal(holdingId, Assert.Single(snapshot.Holdings).HoldingId);
    }

    [Fact]
    public async Task AddingToAMonthWithoutABalanceIsRejected()
    {
        var budget = Seeded(ABudget());

        var result = await new AddHoldingHandler(_budgets, _snapshots, _work, _clock).HandleAsync(
            new AddHolding(Guid.NewGuid(), Owner, budget.Id, October, Guid.NewGuid(), "Konto", HoldingType.Cash,
                new Money(10m, "PLN", 1m, Today)), CancellationToken.None);

        Assert.Equal("That month has no balance yet.", result.Rejection);
        Assert.Empty(_work.Staged);
    }

    [Fact]
    public async Task AnArchivedBudgetRejectsAHolding()
    {
        var budget = Seeded(ABudget());
        Seeded(ASnapshot(budget, October));
        Archive(budget);

        var result = await new AddHoldingHandler(_budgets, _snapshots, _work, _clock).HandleAsync(
            new AddHolding(Guid.NewGuid(), Owner, budget.Id, October, Guid.NewGuid(), "Konto", HoldingType.Cash,
                new Money(10m, "PLN", 1m, Today)), CancellationToken.None);

        Assert.Equal("This budget is archived.", result.Rejection);
        Assert.Empty(_work.Staged);
    }

    [Fact]
    public async Task EditingAHoldingStagesTheMonth()
    {
        var budget = Seeded(ABudget());
        var wallet = AHolding();
        var snapshot = Seeded(ASnapshot(budget, October, [wallet]));

        var result = await new EditHoldingHandler(_budgets, _snapshots, _work, _clock).HandleAsync(
            new EditHolding(Guid.NewGuid(), Owner, budget.Id, October, wallet.HoldingId, "Cash box", HoldingType.Cash,
                wallet.Money), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.IsType<HoldingEdited>(Assert.Single(Assert.Single(_work.Staged).Events));
        Assert.Equal("Cash box", Assert.Single(snapshot.Holdings).Name);
    }

    [Fact]
    public async Task RemovingAHoldingStagesTheMonth()
    {
        var budget = Seeded(ABudget());
        var wallet = AHolding();
        var snapshot = Seeded(ASnapshot(budget, October, [wallet]));

        var result = await new RemoveHoldingHandler(_budgets, _snapshots, _work, _clock).HandleAsync(
            new RemoveHolding(Guid.NewGuid(), Owner, budget.Id, October, wallet.HoldingId), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.IsType<HoldingRemoved>(Assert.Single(Assert.Single(_work.Staged).Events));
        Assert.Empty(snapshot.Holdings);
    }

    [Fact]
    public async Task DeletingAMonthStagesTheSoftDelete()
    {
        var budget = Seeded(ABudget());
        var snapshot = Seeded(ASnapshot(budget, October, [AHolding()]));

        var result = await new DeleteBalanceMonthHandler(_budgets, _snapshots, _work, _clock).HandleAsync(
            new DeleteBalanceMonth(Guid.NewGuid(), Owner, budget.Id, October), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.IsType<BalanceMonthDeleted>(Assert.Single(Assert.Single(_work.Staged).Events));
        Assert.True(snapshot.Deleted);
    }

    [Fact]
    public async Task AMissingBudgetIsRejectedBeforeAnySnapshotIsRead()
    {
        var budget = ABudget();
        Seeded(ASnapshot(budget, October));

        var result = await new DeleteBalanceMonthHandler(_budgets, _snapshots, _work, _clock).HandleAsync(
            new DeleteBalanceMonth(Guid.NewGuid(), Owner, budget.Id, October), CancellationToken.None);

        Assert.Equal("That budget does not exist.", result.Rejection);
        Assert.Empty(_work.Staged);
        Assert.Equal(0, _snapshots.Loads);
    }
}
