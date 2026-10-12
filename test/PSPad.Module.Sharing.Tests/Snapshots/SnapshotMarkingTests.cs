using Microsoft.Extensions.Logging.Abstractions;
using PSPad.Module.Sharing.Snapshots;
using PSPad.Module.Sharing.Tests.Fakes;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Sharing.Tests.Snapshots;

[UnitTest]
public class SnapshotMarkingTests
{
    static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid SnapshotId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    static readonly Guid TaskId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    static readonly Guid StepId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    static readonly Guid ItemId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    static readonly Guid ListId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    const string Token = "k3Jv9s2mQ0x7b1nR4tYw8eZa";
    static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MarkFailsWithNotFoundForAnUnknownToken()
    {
        var store = new InMemorySnapshotStore();
        var commands = new RecordingServerCommands();
        var marking = NewMarking(store, commands);

        var outcome = await marking.MarkAsync("missing-token", TaskId, null, true, CancellationToken.None);

        Assert.Equal(MarkOutcome.NotFound, outcome);
        Assert.Empty(commands.Received);
    }

    [Fact]
    public async Task MarkFailsWithNotFoundForAnExpiredSnapshot()
    {
        var store = new InMemorySnapshotStore();
        await store.SaveAsync(TasksSnapshot(Now.AddSeconds(-1)), CancellationToken.None);
        var commands = new RecordingServerCommands();
        var marking = NewMarking(store, commands);

        var outcome = await marking.MarkAsync(Token, TaskId, null, true, CancellationToken.None);

        Assert.Equal(MarkOutcome.NotFound, outcome);
        Assert.Empty(commands.Received);
    }

    [Fact]
    public async Task MarkFailsWithNotFoundForAnEntryTheSnapshotDoesNotHold()
    {
        var store = new InMemorySnapshotStore();
        await store.SaveAsync(TasksSnapshot(Now.AddDays(1)), CancellationToken.None);
        var marking = NewMarking(store, new RecordingServerCommands());

        var outcome = await marking.MarkAsync(Token, Guid.NewGuid(), null, true, CancellationToken.None);

        Assert.Equal(MarkOutcome.NotFound, outcome);
    }

    [Fact]
    public async Task MarkReturnsUnchangedWhenTheMarkDoesNotChangeAnything()
    {
        var snapshot = TasksSnapshot(Now.AddDays(1)).WithMark(TaskId, null, true, Now);
        var store = new InMemorySnapshotStore();
        await store.SaveAsync(snapshot, CancellationToken.None);
        var commands = new RecordingServerCommands();
        var marking = NewMarking(store, commands);

        var outcome = await marking.MarkAsync(Token, TaskId, null, true, CancellationToken.None);

        Assert.Equal(MarkOutcome.Unchanged, outcome);
        Assert.Empty(commands.Received);
    }

    [Fact]
    public async Task MarkSavesTheSnapshotAndRunsMarkTaskFromSnapshotCarryingTheOwner()
    {
        var store = new InMemorySnapshotStore();
        await store.SaveAsync(TasksSnapshot(Now.AddDays(1)), CancellationToken.None);
        var commands = new RecordingServerCommands();
        var marking = NewMarking(store, commands);

        var outcome = await marking.MarkAsync(Token, TaskId, null, true, CancellationToken.None);

        Assert.Equal(MarkOutcome.Marked, outcome);
        var saved = await store.FindAsync(SnapshotId, CancellationToken.None);
        Assert.True(Assert.Single(saved!.Tasks).Marked);
        var command = Assert.Single(commands.Received);
        var markCommand = Assert.IsType<MarkTaskFromSnapshot>(command);
        Assert.Equal(Owner, markCommand.UserId);
        Assert.Equal(TaskId, markCommand.TaskId);
        Assert.Null(markCommand.StepId);
        Assert.Equal(SnapshotId, markCommand.SnapshotId);
        Assert.True(markCommand.Marked);
    }

    [Fact]
    public async Task AStepMarkSendsTheStepId()
    {
        var store = new InMemorySnapshotStore();
        await store.SaveAsync(TasksSnapshotWithStep(Now.AddDays(1)), CancellationToken.None);
        var commands = new RecordingServerCommands();
        var marking = NewMarking(store, commands);

        var outcome = await marking.MarkAsync(Token, TaskId, StepId, true, CancellationToken.None);

        Assert.Equal(MarkOutcome.Marked, outcome);
        var markCommand = Assert.IsType<MarkTaskFromSnapshot>(Assert.Single(commands.Received));
        Assert.Equal(StepId, markCommand.StepId);
    }

    [Fact]
    public async Task MarkRunsMarkReferenceItemFromSnapshotForAReferenceSnapshot()
    {
        var store = new InMemorySnapshotStore();
        await store.SaveAsync(ReferenceSnapshot(Now.AddDays(1)), CancellationToken.None);
        var commands = new RecordingServerCommands();
        var marking = NewMarking(store, commands);

        var outcome = await marking.MarkAsync(Token, ItemId, null, true, CancellationToken.None);

        Assert.Equal(MarkOutcome.Marked, outcome);
        var markCommand = Assert.IsType<MarkReferenceItemFromSnapshot>(Assert.Single(commands.Received));
        Assert.Equal(Owner, markCommand.UserId);
        Assert.Equal(ItemId, markCommand.ItemId);
    }

    [Fact]
    public async Task ARejectedCommandStillLeavesTheSnapshotMarked()
    {
        var store = new InMemorySnapshotStore();
        await store.SaveAsync(TasksSnapshot(Now.AddDays(1)), CancellationToken.None);
        var commands = new RecordingServerCommands { RejectWith = "List not found" };
        var marking = NewMarking(store, commands);

        var outcome = await marking.MarkAsync(Token, TaskId, null, true, CancellationToken.None);

        Assert.Equal(MarkOutcome.Marked, outcome);
        var saved = await store.FindAsync(SnapshotId, CancellationToken.None);
        Assert.True(Assert.Single(saved!.Tasks).Marked);
    }

    [Fact]
    public async Task AListTakesAHundredMarkChangesADayThenThrottles()
    {
        var store = new InMemorySnapshotStore();
        await store.SaveAsync(TasksSnapshot(Now.AddDays(1)), CancellationToken.None);
        var commands = new RecordingServerCommands();
        var marking = NewMarking(store, commands, new InMemoryMarkBudget());

        for (var change = 0; change < SnapshotMarking.DailyMarksPerList; change++)
        {
            Assert.Equal(MarkOutcome.Marked,
                await marking.MarkAsync(Token, TaskId, null, change % 2 == 0, CancellationToken.None));
        }

        var outcome = await marking.MarkAsync(Token, TaskId, null, true, CancellationToken.None);

        Assert.Equal(MarkOutcome.Throttled, outcome);
        Assert.Equal(SnapshotMarking.DailyMarksPerList, commands.Received.Count);
        var saved = await store.FindAsync(SnapshotId, CancellationToken.None);
        Assert.False(Assert.Single(saved!.Tasks).Marked);
    }

    [Fact]
    public async Task TheBudgetIsSpentOnTheSnapshotsListForTheUtcDay()
    {
        var store = new InMemorySnapshotStore();
        await store.SaveAsync(TasksSnapshot(Now.AddDays(1)), CancellationToken.None);
        var budget = new InMemoryMarkBudget();
        var marking = NewMarking(store, new RecordingServerCommands(), budget);

        await marking.MarkAsync(Token, TaskId, null, true, CancellationToken.None);

        Assert.Equal(1, budget.SpentOn(ListId, DateOnly.FromDateTime(Now.UtcDateTime)));
    }

    [Fact]
    public async Task AnUnchangedMarkSpendsNoBudget()
    {
        var snapshot = TasksSnapshot(Now.AddDays(1)).WithMark(TaskId, null, true, Now);
        var store = new InMemorySnapshotStore();
        await store.SaveAsync(snapshot, CancellationToken.None);
        var budget = new InMemoryMarkBudget();
        var marking = NewMarking(store, new RecordingServerCommands(), budget);

        await marking.MarkAsync(Token, TaskId, null, true, CancellationToken.None);

        Assert.Equal(0, budget.SpentOn(ListId, DateOnly.FromDateTime(Now.UtcDateTime)));
    }

    static SnapshotMarking NewMarking(InMemorySnapshotStore store, RecordingServerCommands commands) =>
        NewMarking(store, commands, new InMemoryMarkBudget());

    static SnapshotMarking NewMarking(
        InMemorySnapshotStore store, RecordingServerCommands commands, InMemoryMarkBudget budget) =>
        new(store, budget, commands, new FixedClock(Now), NullLogger<SnapshotMarking>.Instance);

    static ListSnapshot TasksSnapshot(DateTimeOffset expiresAt) => new()
    {
        Id = SnapshotId,
        Token = Token,
        UserId = Owner,
        ListId = ListId,
        Kind = ListKind.Tasks,
        Name = "Groceries",
        CreatedAt = Now,
        ExpiresAt = expiresAt,
        Tasks = [new SnapshotTask(TaskId, "Buy milk", false, null, Priority.None, false, "", false, null, [])]
    };

    static ListSnapshot TasksSnapshotWithStep(DateTimeOffset expiresAt) =>
        TasksSnapshot(expiresAt) with
        {
            Tasks = [new SnapshotTask(
                TaskId, "Trip", false, null, Priority.None, false, "", false, null,
                [new SnapshotStep(StepId, "Pack", false, false, null)])]
        };

    static ListSnapshot ReferenceSnapshot(DateTimeOffset expiresAt) => new()
    {
        Id = SnapshotId,
        Token = Token,
        UserId = Owner,
        ListId = ListId,
        Kind = ListKind.Reference,
        Name = "Filaments",
        CreatedAt = Now,
        ExpiresAt = expiresAt,
        Items = [new SnapshotItem(ItemId, "Filament", "", false, false, null, [])]
    };
}
