using PSPad.Module.Sharing.Snapshots;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Sharing.Tests.Snapshots;

[UnitTest]
public class SnapshotBuilderTests
{
    static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid AreaId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly Guid ListId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    static readonly Guid SnapshotId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    const string Token = "k3Jv9s2mQ0x7b1nR4tYw8eZa";
    static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset ExpiresAt = Now.AddDays(7);
    static readonly DateOnly Today = new(2026, 9, 28);

    [Fact]
    public void ItCopiesNameKindAndExpiry()
    {
        var snapshot = Build(TasksList(), [], []);

        Assert.Equal("Groceries", snapshot.Name);
        Assert.Equal(ListKind.Tasks, snapshot.Kind);
        Assert.Equal(ExpiresAt, snapshot.ExpiresAt);
        Assert.Equal(Owner, snapshot.UserId);
        Assert.Equal(ListId, snapshot.ListId);
        Assert.Equal(SnapshotId, snapshot.Id);
        Assert.Equal(Token, snapshot.Token);
    }

    [Fact]
    public void ItExcludesDeletedAndOtherListsTasks()
    {
        var kept = NewTask("Keep");
        var deleted = NewTask("Gone");
        deleted.ApplyAll(TodoTask.Decide(deleted, new DeleteTask(Guid.NewGuid(), Owner, deleted.Id), Now));
        var elsewhere = NewTask("Elsewhere", listId: Guid.NewGuid());

        var snapshot = Build(TasksList(), [kept, deleted, elsewhere], []);

        Assert.Equal("Keep", Assert.Single(snapshot.Tasks).Name);
    }

    [Fact]
    public void ItExcludesDeletedAndOtherListsItems()
    {
        var kept = NewItem("Keep");
        var deleted = NewItem("Gone");
        deleted.ApplyAll(ReferenceItem.Decide(deleted, new DeleteReferenceItem(Guid.NewGuid(), Owner, deleted.Id), Now));
        var elsewhere = NewItem("Elsewhere", listId: Guid.NewGuid());

        var snapshot = Build(ReferenceList(), [], [kept, deleted, elsewhere]);

        Assert.Equal("Keep", Assert.Single(snapshot.Items).Name);
    }

    [Fact]
    public void OpenTasksComeBeforeDoneOnes()
    {
        var done = NewTask("Done");
        done.ApplyAll(TodoTask.Decide(done, new CompleteTask(Guid.NewGuid(), Owner, done.Id), Now));
        var open = NewTask("Open");

        var snapshot = Build(TasksList(), [done, open], []);

        Assert.Equal(["Open", "Done"], snapshot.Tasks.Select(task => task.Name));
    }

    [Fact]
    public void StarredTasksComeFirstWithinTheSameGroup()
    {
        var plain = NewTask("Plain");
        var starred = NewTask("Starred");
        starred.ApplyAll(TodoTask.Decide(starred, new StarTask(Guid.NewGuid(), Owner, starred.Id, true), Now));

        var snapshot = Build(TasksList(), [plain, starred], []);

        Assert.Equal(["Starred", "Plain"], snapshot.Tasks.Select(task => task.Name));
    }

    [Fact]
    public void TasksWithNoDueDateSortAfterDatedOnes()
    {
        var dated = NewTask("Dated");
        dated.ApplyAll(TodoTask.Decide(
            dated, new SetTaskDueDate(Guid.NewGuid(), Owner, dated.Id, new DateOnly(2026, 9, 29)), Now));
        var undated = NewTask("Undated");

        var snapshot = Build(TasksList(), [undated, dated], []);

        Assert.Equal(["Dated", "Undated"], snapshot.Tasks.Select(task => task.Name));
    }

    [Fact]
    public void TasksWithTheSameDueDateSortByCreationOrder()
    {
        var first = NewTask("First", createdAt: Now);
        var second = NewTask("Second", createdAt: Now.AddMinutes(1));

        var snapshot = Build(TasksList(), [second, first], []);

        Assert.Equal(["First", "Second"], snapshot.Tasks.Select(task => task.Name));
    }

    [Fact]
    public void TasksCreatedAtTheSameMomentSortById()
    {
        var lower = NewTask("A", id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var higher = NewTask("B", id: Guid.Parse("00000000-0000-0000-0000-000000000002"));

        var snapshot = Build(TasksList(), [higher, lower], []);

        Assert.Equal(["A", "B"], snapshot.Tasks.Select(task => task.Name));
    }

    [Fact]
    public void ACompletedTaskIsDone()
    {
        var task = NewTask("Buy milk");
        task.ApplyAll(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), Owner, task.Id), Now));

        var snapshot = Build(TasksList(), [task], []);

        Assert.True(Assert.Single(snapshot.Tasks).Done);
    }

    [Fact]
    public void ARepeatingTaskIsOpen()
    {
        var task = NewTask("Read a book");
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), Owner, task.Id, RecurrenceRule.Daily(Today)), Now));

        var snapshot = Build(TasksList(), [task], []);

        Assert.False(Assert.Single(snapshot.Tasks).Done);
    }

    [Fact]
    public void TheSnapshotTaskCarriesNoGoalOrRecurrenceData()
    {
        var task = NewTask("Buy milk");
        task.ApplyAll(TodoTask.Decide(task, new LinkTaskToGoal(Guid.NewGuid(), Owner, task.Id, Guid.NewGuid()), Now));

        var snapshot = Build(TasksList(), [task], []);

        var expected = new SnapshotTask(
            task.Id, "Buy milk", false, null, Priority.None, false, "", false, null, []);
        Assert.Equal(expected, Assert.Single(snapshot.Tasks));
    }

    [Fact]
    public void StepsComeInPositionOrder()
    {
        var task = NewTask("Trip");
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        task.ApplyAll(TodoTask.Decide(task, new AddStep(Guid.NewGuid(), Owner, task.Id, first, "Pack"), Now));
        task.ApplyAll(TodoTask.Decide(task, new AddStep(Guid.NewGuid(), Owner, task.Id, second, "Book"), Now));
        task.ApplyAll(TodoTask.Decide(task, new MoveStep(Guid.NewGuid(), Owner, task.Id, second, 0), Now));

        var snapshot = Build(TasksList(), [task], []);

        Assert.Equal(["Book", "Pack"], Assert.Single(snapshot.Tasks).Steps.Select(step => step.Name));
    }

    [Fact]
    public void StarredItemsComeFirstThenByPosition()
    {
        var first = NewItem("First", position: 1);
        var second = NewItem("Second", position: 0);
        var third = NewItem("Third", position: 2);
        third.ApplyAll(ReferenceItem.Decide(third, new StarReferenceItem(Guid.NewGuid(), Owner, third.Id, true), Now));

        var snapshot = Build(ReferenceList(), [], [first, second, third]);

        Assert.Equal(["Third", "Second", "First"], snapshot.Items.Select(item => item.Name));
    }

    [Fact]
    public void FieldsComeInPositionOrder()
    {
        var item = NewItem("Filament");
        var colour = Guid.NewGuid();
        var weight = Guid.NewGuid();
        item.ApplyAll(ReferenceItem.Decide(item, new AddReferenceField(Guid.NewGuid(), Owner, item.Id, colour, "Colour", "black", null), Now));
        item.ApplyAll(ReferenceItem.Decide(item, new AddReferenceField(Guid.NewGuid(), Owner, item.Id, weight, "Weight", "1kg", null), Now));
        item.ApplyAll(ReferenceItem.Decide(item, new MoveReferenceField(Guid.NewGuid(), Owner, item.Id, weight, 0), Now));

        var snapshot = Build(ReferenceList(), [], [item]);

        Assert.Equal(["Weight", "Colour"], Assert.Single(snapshot.Items).Fields.Select(field => field.Label));
    }

    [Fact]
    public void WithMarkMarksATask()
    {
        var task = NewTask("Buy milk");
        var snapshot = Build(TasksList(), [task], []);

        var marked = snapshot.WithMark(task.Id, null, true, Now);

        var snapshotTask = Assert.Single(marked.Tasks);
        Assert.True(snapshotTask.Marked);
        Assert.Equal(Now, snapshotTask.MarkedAt);
    }

    [Fact]
    public void WithMarkMarksAStep()
    {
        var task = NewTask("Trip");
        var stepId = Guid.NewGuid();
        task.ApplyAll(TodoTask.Decide(task, new AddStep(Guid.NewGuid(), Owner, task.Id, stepId, "Pack"), Now));
        var snapshot = Build(TasksList(), [task], []);

        var marked = snapshot.WithMark(task.Id, stepId, true, Now);

        var step = Assert.Single(Assert.Single(marked.Tasks).Steps);
        Assert.True(step.Marked);
        Assert.Equal(Now, step.MarkedAt);
    }

    [Fact]
    public void WithMarkMarksAnItem()
    {
        var item = NewItem("Filament");
        var snapshot = Build(ReferenceList(), [], [item]);

        var marked = snapshot.WithMark(item.Id, null, true, Now);

        var snapshotItem = Assert.Single(marked.Items);
        Assert.True(snapshotItem.Marked);
        Assert.Equal(Now, snapshotItem.MarkedAt);
    }

    [Fact]
    public void WithMarkReturnsTheSameInstanceWhenNothingChanges()
    {
        var task = NewTask("Buy milk");
        var snapshot = Build(TasksList(), [task], []);
        var marked = snapshot.WithMark(task.Id, null, true, Now);

        var unchanged = marked.WithMark(task.Id, null, true, Now.AddMinutes(1));

        Assert.Same(marked, unchanged);
    }

    [Fact]
    public void WithMarkReturnsTheSameInstanceForAnUnknownEntry()
    {
        var task = NewTask("Buy milk");
        var snapshot = Build(TasksList(), [task], []);

        var result = snapshot.WithMark(Guid.NewGuid(), null, true, Now);

        Assert.Same(snapshot, result);
    }

    [Fact]
    public void IsLiveAtReflectsExpiry()
    {
        var snapshot = Build(TasksList(), [], []);

        Assert.True(snapshot.IsLiveAt(ExpiresAt.AddSeconds(-1)));
        Assert.False(snapshot.IsLiveAt(ExpiresAt));
        Assert.False(snapshot.IsLiveAt(ExpiresAt.AddSeconds(1)));
    }

    [Fact]
    public void ItCarriesTheOwnerName() =>
        Assert.Equal("Łukasz", Build(TasksList(), [], []).OwnerName);

    [Fact]
    public void ItCountsTopLevelEntriesAndTicks()
    {
        var snapshot = Build(TasksList(), [], []) with
        {
            Tasks =
            [
                new SnapshotTask(Guid.NewGuid(), "A", false, null, Priority.None, false, "", true, Now, []),
                new SnapshotTask(Guid.NewGuid(), "B", false, null, Priority.None, false, "", false, null, []),
                new SnapshotTask(Guid.NewGuid(), "C", false, null, Priority.None, false, "", true, Now, [])
            ]
        };

        Assert.Equal(3, snapshot.EntryCount);
        Assert.Equal(2, snapshot.TickCount);
    }

    static ListSnapshot Build(TaskList list, IEnumerable<TodoTask> tasks, IEnumerable<ReferenceItem> items) =>
        SnapshotBuilder.Build(SnapshotId, Token, list, tasks, items, Now, ExpiresAt, Today, "Łukasz");

    static TaskList TasksList()
    {
        var list = new TaskList();
        list.Apply(new TaskListCreated(ListId, Owner, Now, AreaId, "Groceries", ListKind.Tasks));
        return list;
    }

    static TaskList ReferenceList()
    {
        var list = new TaskList();
        list.Apply(new TaskListCreated(ListId, Owner, Now, AreaId, "Filaments", ListKind.Reference));
        return list;
    }

    static TodoTask NewTask(string name, Guid? listId = null, Guid? id = null, DateTimeOffset? createdAt = null)
    {
        var task = new TodoTask();
        task.Apply(new TaskCreated(id ?? Guid.NewGuid(), Owner, createdAt ?? Now, listId ?? ListId, name));
        return task;
    }

    static ReferenceItem NewItem(string name, Guid? listId = null, int position = 0)
    {
        var item = new ReferenceItem();
        item.ApplyAll(ReferenceItem.Decide(
            null, new CreateReferenceItem(Guid.NewGuid(), Owner, Guid.NewGuid(), listId ?? ListId, name, position), Now));
        return item;
    }
}
