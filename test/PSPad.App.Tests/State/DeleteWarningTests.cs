using PSPad.App.State;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class DeleteWarningTests
{
    static readonly Guid User = Guid.NewGuid();
    static readonly Guid AreaId = Guid.NewGuid();

    [Fact]
    public void AnAreaNamesHowManyListsAndTasksGoWithIt()
    {
        var errands = NewList(AreaId);
        var chores = NewList(AreaId);
        TodoTask[] tasks = [NewTask(errands.Id), NewTask(errands.Id), NewTask(chores.Id)];

        var message = DeleteWarning.ForArea("Dom", AreaId, [errands, chores], tasks);

        Assert.Equal("Delete “Dom” and its 2 lists and 3 tasks? This can’t be undone.", message);
    }

    [Fact]
    public void AnAreaCountsInTheSingular()
    {
        var errands = NewList(AreaId);

        var message = DeleteWarning.ForArea("Dom", AreaId, [errands], [NewTask(errands.Id)]);

        Assert.Equal("Delete “Dom” and its 1 list and 1 task? This can’t be undone.", message);
    }

    [Fact]
    public void AnAreaWithEmptyListsMentionsOnlyTheLists()
    {
        var message = DeleteWarning.ForArea("Dom", AreaId, [NewList(AreaId), NewList(AreaId)], []);

        Assert.Equal("Delete “Dom” and its 2 lists? This can’t be undone.", message);
    }

    [Fact]
    public void AnEmptyAreaMentionsNothingElse()
    {
        var message = DeleteWarning.ForArea("Dom", AreaId, [], []);

        Assert.Equal("Delete “Dom”? This can’t be undone.", message);
    }

    [Fact]
    public void AnAreaIgnoresOtherAreasAndDeletedChildren()
    {
        var mine = NewList(AreaId);
        var gone = NewList(AreaId, deleted: true);
        var elsewhere = NewList(Guid.NewGuid());
        TodoTask[] tasks = [NewTask(mine.Id), NewTask(mine.Id, deleted: true), NewTask(gone.Id), NewTask(elsewhere.Id)];

        var message = DeleteWarning.ForArea("Dom", AreaId, [mine, gone, elsewhere], tasks);

        Assert.Equal("Delete “Dom” and its 1 list and 1 task? This can’t be undone.", message);
    }

    [Fact]
    public void AListNamesHowManyLiveTasksGoWithIt()
    {
        var list = NewList(AreaId);
        TodoTask[] tasks = [NewTask(list.Id), NewTask(list.Id), NewTask(list.Id, deleted: true), NewTask(Guid.NewGuid())];

        var message = DeleteWarning.ForList("Zakupy", list.Id, tasks);

        Assert.Equal("Delete “Zakupy” and its 2 tasks? This can’t be undone.", message);
    }

    [Fact]
    public void AnEmptyListMentionsNothingElse()
    {
        var message = DeleteWarning.ForList("Zakupy", Guid.NewGuid(), Array.Empty<TodoTask>());

        Assert.Equal("Delete “Zakupy”? This can’t be undone.", message);
    }

    [Fact]
    public void AReferenceListNamesHowManyLiveItemsGoWithIt()
    {
        var list = NewList(AreaId);
        ReferenceItem[] items = [NewItem(list.Id), NewItem(list.Id), NewItem(list.Id, deleted: true), NewItem(Guid.NewGuid())];

        var message = DeleteWarning.ForList("Przepisy", list.Id, items);

        Assert.Equal("Delete “Przepisy” and its 2 items? This can’t be undone.", message);
    }

    [Fact]
    public void AnAreaNamesListsTasksAndItemsTogether()
    {
        var tasksList = NewList(AreaId);
        var referenceList = NewList(AreaId);
        TodoTask[] tasks = [NewTask(tasksList.Id)];
        ReferenceItem[] items = [NewItem(referenceList.Id), NewItem(referenceList.Id)];

        var message = DeleteWarning.ForArea("Dom", AreaId, [tasksList, referenceList], tasks, items);

        Assert.Equal("Delete “Dom” and its 2 lists, 1 task and 2 items? This can’t be undone.", message);
    }

    static ReferenceItem NewItem(Guid listId, bool deleted = false)
    {
        var item = new ReferenceItem();
        var id = Guid.NewGuid();
        item.ApplyAll(ReferenceItem.Decide(
            null, new CreateReferenceItem(Guid.NewGuid(), User, id, listId, "Item", 0), DateTimeOffset.UnixEpoch));

        if (deleted)
        {
            item.ApplyAll(ReferenceItem.Decide(item, new DeleteReferenceItem(Guid.NewGuid(), User, id), DateTimeOffset.UnixEpoch));
        }

        return item;
    }

    static TaskList NewList(Guid areaId, bool deleted = false)
    {
        var list = new TaskList();
        var id = Guid.NewGuid();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, id, areaId, "List", 0), DateTimeOffset.UnixEpoch));

        if (deleted)
        {
            list.ApplyAll(TaskList.Decide(list, new DeleteTaskList(Guid.NewGuid(), User, id), DateTimeOffset.UnixEpoch));
        }

        return list;
    }

    static TodoTask NewTask(Guid listId, bool deleted = false)
    {
        var task = new TodoTask();
        var id = Guid.NewGuid();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, id, listId, "Task"), DateTimeOffset.UnixEpoch));

        if (deleted)
        {
            task.ApplyAll(TodoTask.Decide(task, new DeleteTask(Guid.NewGuid(), User, id), DateTimeOffset.UnixEpoch));
        }

        return task;
    }
}
