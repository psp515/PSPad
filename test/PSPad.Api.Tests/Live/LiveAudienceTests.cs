using PSPad.Abstractions;
using PSPad.Api.Live;
using PSPad.Module.Money;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Values;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Live;

[UnitTest]
public class LiveAudienceTests
{
    static readonly DateTimeOffset At = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    readonly FakeContainerMembers _containers = new();
    readonly Guid _owner = Guid.NewGuid();
    readonly Guid _member = Guid.NewGuid();
    readonly Guid _actor = Guid.NewGuid();

    LiveAudience Audience => new(_containers);

    static CommittedChange Change<T>(T aggregate, params DomainEvent[] events) where T : Aggregate
    {
        aggregate.ApplyAll(events);
        return new CommittedChange(aggregate, events);
    }

    TaskList SharedList(Guid listId, params Guid[] members)
    {
        var list = new TaskList();
        list.ApplyAll([
            new TaskListCreated(listId, _owner, At, Guid.NewGuid(), "Books"),
            new TaskListShared(listId, _owner, At, "token", "Owner", "K7M4PX"),
            .. members.Select(member => (DomainEvent)new TaskListJoined(listId, member, At, member, "Member"))
        ]);
        return list;
    }

    [Fact]
    public async Task AnOwnedAggregateReachesTheActorAndTheOwner()
    {
        var area = Change(new Area(), new AreaCreated(Guid.NewGuid(), _owner, At, "Home", 0));

        var audience = await Audience.ForAsync(_actor, [area], CancellationToken.None);

        Assert.Equal(new[] { _actor, _owner }.Order(), audience.Order());
        Assert.Equal(0, _containers.Lookups);
    }

    [Fact]
    public async Task AListReachesItsMembersAndAnyoneTheCommitRemoved()
    {
        var listId = Guid.NewGuid();
        var removed = Guid.NewGuid();
        var list = SharedList(listId, _member, removed);
        var removal = new ListMemberRemoved(listId, _owner, At, removed);
        list.ApplyAll([removal]);

        var audience = await Audience.ForAsync(_owner, [new CommittedChange(list, [removal])], CancellationToken.None);

        Assert.Equal(new[] { _owner, _member, removed }.Order(), audience.Order());
    }

    [Fact]
    public async Task ATaskReachesTheOwnerAndMembersOfItsListAndItsPreviousList()
    {
        var listId = Guid.NewGuid();
        var previousId = Guid.NewGuid();
        var previousMember = Guid.NewGuid();
        _containers.Lists[listId] = new ContainerMembers(_owner, [_member]);
        _containers.Lists[previousId] = new ContainerMembers(_owner, [previousMember]);
        var task = new TodoTask();
        task.ApplyAll([new TaskCreated(Guid.NewGuid(), _member, At, previousId, "Read")]);
        var move = Change(task, new TaskMovedToList(task.Id, _member, At, listId, "Read"));

        var audience = await Audience.ForAsync(_member, [move], CancellationToken.None);

        Assert.Equal(new[] { _owner, _member, previousMember }.Order(), audience.Order());
    }

    [Fact]
    public async Task AReferenceItemReachesTheOwnerAndMembersOfItsList()
    {
        var listId = Guid.NewGuid();
        _containers.Lists[listId] = new ContainerMembers(_owner, [_member]);
        var item = Change(new ReferenceItem(), new ReferenceItemCreated(Guid.NewGuid(), _member, At, listId, "Dune", 0));

        var audience = await Audience.ForAsync(_member, [item], CancellationToken.None);

        Assert.Equal(new[] { _owner, _member }.Order(), audience.Order());
    }

    [Fact]
    public async Task ABudgetReachesItsMembersAndAnyoneTheCommitRemoved()
    {
        var budgetId = Guid.NewGuid();
        var removed = Guid.NewGuid();
        var budget = new Budget();
        budget.ApplyAll([
            new BudgetCreated(budgetId, _owner, At, "Home", ["Food"], ["Salary"]),
            new BudgetShared(budgetId, _owner, At, "token", "K7M4PX", "Owner"),
            new BudgetJoined(budgetId, _member, At, _member, "Member"),
            new BudgetJoined(budgetId, removed, At, removed, "Gone")
        ]);
        var removal = new BudgetMemberRemoved(budgetId, _owner, At, removed);
        budget.ApplyAll([removal]);

        var audience = await Audience.ForAsync(_owner, [new CommittedChange(budget, [removal])], CancellationToken.None);

        Assert.Equal(new[] { _owner, _member, removed }.Order(), audience.Order());
    }

    [Fact]
    public async Task AnEntryAndABalanceReachTheOwnerAndMembersOfTheirBudget()
    {
        var budgetId = Guid.NewGuid();
        _containers.Budgets[budgetId] = new ContainerMembers(_owner, [_member]);
        var today = DateOnly.FromDateTime(At.UtcDateTime);
        var entry = Change(new MoneyEntry(), new ExpenseRecorded(
            Guid.NewGuid(), _member, At, budgetId, "Bread", "Food", new Money(10m, "PLN", 1m, today), today, null));
        var balance = Change(new BalanceSnapshot(), new BalanceMonthStarted(
            Guid.NewGuid(), _member, At, budgetId, new YearMonth(2026, 10), []));

        var audience = await Audience.ForAsync(_member, [entry, balance], CancellationToken.None);

        Assert.Equal(new[] { _owner, _member }.Order(), audience.Order());
        Assert.Equal(1, _containers.Lookups);
    }

    [Fact]
    public async Task AnEmptyIdIsNeverInTheAudience()
    {
        var area = Change(new Area(), new AreaCreated(Guid.NewGuid(), _owner, At, "Home", 0));

        var audience = await Audience.ForAsync(Guid.Empty, [area], CancellationToken.None);

        Assert.Equal([_owner], audience.ToArray());
    }
}
