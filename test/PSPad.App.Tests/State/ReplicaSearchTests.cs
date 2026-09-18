using PSPad.Abstractions;
using PSPad.App.State;
using PSPad.App.State.Replica;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class ReplicaSearchTests
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public async Task ItMatchesTasksByNameCaseInsensitively()
    {
        var search = Arrange(out var area, out var list, "Kupić farbę", "Oddać klucze");

        var hits = await search.FindAsync(User, "FARB");

        Assert.Single(hits, hit => hit.Name == "Kupić farbę");
    }

    [Fact]
    public async Task ATaskHitCarriesItsAreaAndListPath()
    {
        var search = Arrange(out _, out _, "Kupić farbę");

        var hit = Assert.Single(await search.FindAsync(User, "farb"));

        Assert.Equal("Dom › Remont", hit.Path);
        Assert.False(hit.IsList);
    }

    [Fact]
    public async Task ItMatchesListsToo()
    {
        var search = Arrange(out _, out _);

        var hit = Assert.Single(await search.FindAsync(User, "remo"));

        Assert.Equal("Remont", hit.Name);
        Assert.True(hit.IsList);
        Assert.Equal("Dom", hit.Path);
    }

    [Fact]
    public async Task AnEmptyQueryMatchesNothing()
    {
        var search = Arrange(out _, out _, "Kupić farbę");

        Assert.Empty(await search.FindAsync(User, "   "));
    }

    [Fact]
    public async Task DeletedTasksAreNotFound()
    {
        var search = Arrange(out _, out var list, "Kupić farbę");
        var task = Task(list.Id, "Zniknąć");
        task.ApplyAll(TodoTask.Decide(
            task, new DeleteTask(Guid.NewGuid(), User, task.Id), DateTimeOffset.UnixEpoch));
        Save(task);

        Assert.DoesNotContain(await search.FindAsync(User, "znik"), hit => hit.Name == "Zniknąć");
    }

    [Fact]
    public async Task ATaskWhoseListWasDeletedIsNotFound()
    {
        var search = Arrange(out _, out var list, "Kupić farbę");
        list.ApplyAll(TaskList.Decide(
            list, new DeleteTaskList(Guid.NewGuid(), User, list.Id), DateTimeOffset.UnixEpoch));
        Save(list);

        Assert.DoesNotContain(await search.FindAsync(User, "farb"), hit => hit.Name == "Kupić farbę");
    }

    [Fact]
    public async Task AListWhoseAreaWasDeletedIsNotFound()
    {
        var search = Arrange(out var area, out _);
        area.ApplyAll(Area.Decide(
            area, new DeleteArea(Guid.NewGuid(), User, area.Id), DateTimeOffset.UnixEpoch));
        Save(area);

        Assert.DoesNotContain(await search.FindAsync(User, "remo"), hit => hit.Name == "Remont");
    }

    [Fact]
    public async Task ATaskWhoseListsAreaWasDeletedIsNotFound()
    {
        var search = Arrange(out var area, out _, "Kupić farbę");
        area.ApplyAll(Area.Decide(
            area, new DeleteArea(Guid.NewGuid(), User, area.Id), DateTimeOffset.UnixEpoch));
        Save(area);

        Assert.DoesNotContain(await search.FindAsync(User, "farb"), hit => hit.Name == "Kupić farbę");
    }

    [Fact]
    public async Task ADeletedListIsNotFoundAsAHitItself()
    {
        var search = Arrange(out _, out var list);
        list.ApplyAll(TaskList.Decide(
            list, new DeleteTaskList(Guid.NewGuid(), User, list.Id), DateTimeOffset.UnixEpoch));
        Save(list);

        Assert.DoesNotContain(await search.FindAsync(User, "remo"), hit => hit.Name == "Remont");
    }

    InMemoryReplica _replica = new();

    void Save(Aggregate document) => _replica.SaveAsync(document).GetAwaiter().GetResult();

    ReplicaSearch Arrange(out Area area, out TaskList list, params string[] taskNames)
    {
        _replica = new InMemoryReplica();

        area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), "Dom", 0), DateTimeOffset.UnixEpoch));
        Save(area);

        list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null,
            new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), area.Id, "Remont", 0),
            DateTimeOffset.UnixEpoch));
        Save(list);

        foreach (var name in taskNames)
        {
            Save(Task(list.Id, name));
        }

        return new ReplicaSearch(
            new ReplicaDocumentStore<TodoTask>(_replica),
            new ReplicaDocumentStore<TaskList>(_replica),
            new ReplicaDocumentStore<Area>(_replica));
    }

    static TodoTask Task(Guid listId, string name)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null,
            new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), listId, name),
            DateTimeOffset.UnixEpoch));
        return task;
    }
}
