using System.Text.Json;
using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Lists;

[UnitTest]
public class ListKindTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    static TaskList Create(ListKind? kind = null)
    {
        var command = kind is { } chosen
            ? new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "Filaments", 0, chosen)
            : new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "Chores", 0);
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(null, command, Now));
        return list;
    }

    [Fact]
    public void AListIsATaskListUnlessToldOtherwise() => Assert.Equal(ListKind.Tasks, Create().Kind);

    [Fact]
    public void AReferenceListRemembersItsKind() => Assert.Equal(ListKind.Reference, Create(ListKind.Reference).Kind);

    [Fact]
    public void AnEventStoredBeforeKindsExistedReadsAsTasks()
    {
        const string stored = """
            {"AggregateId":"22222222-2222-2222-2222-222222222222","UserId":"11111111-1111-1111-1111-111111111111",
             "At":"2026-09-01T08:00:00+00:00","AreaId":"33333333-3333-3333-3333-333333333333","Name":"Old","Position":0}
            """;

        var created = JsonSerializer.Deserialize<TaskListCreated>(stored)!;

        Assert.Equal(ListKind.Tasks, created.Kind);
    }

    [Fact]
    public void AStoredKindValueOfOneReadsAsReference()
    {
        const string stored = """
            {"AggregateId":"22222222-2222-2222-2222-222222222222","UserId":"11111111-1111-1111-1111-111111111111",
             "At":"2026-09-01T08:00:00+00:00","AreaId":"33333333-3333-3333-3333-333333333333","Name":"Spools","Position":0,"Kind":1}
            """;

        var created = JsonSerializer.Deserialize<TaskListCreated>(stored)!;

        Assert.Equal(ListKind.Reference, created.Kind);
    }

    [Fact]
    public void RenamingKeepsTheKind()
    {
        var list = Create(ListKind.Reference);

        list.ApplyAll(TaskList.Decide(list, new RenameTaskList(Guid.NewGuid(), User, list.Id, "Spools"), Now));

        Assert.Equal(ListKind.Reference, list.Kind);
    }

    [Fact]
    public void ATaskListAcceptsTasksAndRefusesReferences()
    {
        var list = Create();

        Assert.Same(list, TaskList.RequireAcceptsTasks(list, User));
        Assert.Throws<DomainRejectedException>(() => TaskList.RequireAcceptsReferences(list, User));
    }

    [Fact]
    public void AReferenceListAcceptsReferencesAndRefusesTasks()
    {
        var list = Create(ListKind.Reference);

        Assert.Same(list, TaskList.RequireAcceptsReferences(list, User));
        Assert.Throws<DomainRejectedException>(() => TaskList.RequireAcceptsTasks(list, User));
    }
}
