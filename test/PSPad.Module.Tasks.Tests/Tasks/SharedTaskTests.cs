using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Tasks;

[UnitTest]
public class SharedTaskTests
{
    static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Member = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly Guid Stranger = Guid.Parse("33333333-3333-3333-3333-333333333333");
    static readonly Guid ListId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    static readonly Guid TaskId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);
    static readonly ListAccess Access = new(Owner, Member);
    const string Token = "k3Jv9s2mQ0x7b1nR4tYw8eZa";

    [Fact]
    public void AMemberCreatesATaskOwnedByTheListOwner()
    {
        var created = Assert.IsType<TaskCreated>(Assert.Single(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), Member, TaskId, ListId, "Dune"), Now, Access)));
        Assert.Equal(Owner, created.UserId);
    }

    [Fact]
    public void AMemberRenamesCompletesAndTicksTheOwnersTask()
    {
        var task = OwnedTask();
        var renamed = Assert.Single(TodoTask.Decide(
            task, new RenameTask(Guid.NewGuid(), Member, task.Id, "Dune Messiah"), Now, Access));
        Assert.Equal(Owner, renamed.UserId);
        Assert.Single(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), Member, task.Id), Now, Access));
        Assert.Single(TodoTask.Decide(
            task, new AddStep(Guid.NewGuid(), Member, task.Id, Guid.NewGuid(), "Buy"), Now, Access));
        Assert.Single(TodoTask.Decide(task, new DeleteTask(Guid.NewGuid(), Member, task.Id), Now, Access));
    }

    [Fact]
    public void AMemberSetsTheLeadTimeOfTheOwnersTask()
    {
        var set = Assert.Single(TodoTask.Decide(
            OwnedTask(), new SetTaskLeadTime(Guid.NewGuid(), Member, TaskId, LeadTime.Of(2, LeadUnit.Weeks)), Now, Access));
        Assert.Equal(Owner, set.UserId);
    }

    [Fact]
    public void AMemberSetsTheTimeOfTheOwnersTask()
    {
        var events = TodoTask.Decide(
            OwnedTask(), new SetTaskTime(Guid.NewGuid(), Member, TaskId, TaskTime.Of(new TimeOnly(9, 0), null)), Now, Access);

        Assert.Equal(Owner, Assert.IsType<TaskTimeSet>(Assert.Single(events)).UserId);
    }

    [Fact]
    public void WithoutAccessAMemberIsStillAStranger() =>
        Assert.Throws<DomainRejectedException>(() =>
            TodoTask.Decide(OwnedTask(), new RenameTask(Guid.NewGuid(), Member, TaskId, "X"), Now));

    [Fact]
    public void AccessToAnotherOwnersListDoesNotReachThisTask() =>
        Assert.Throws<DomainRejectedException>(() =>
            TodoTask.Decide(
                OwnedTask(), new RenameTask(Guid.NewGuid(), Member, TaskId, "X"), Now, new ListAccess(Stranger, Member)));

    static TodoTask OwnedTask()
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(null, new CreateTask(Guid.NewGuid(), Owner, TaskId, ListId, "Dune"), Now));
        return task;
    }

    static TaskList ListWithMember()
    {
        var list = new TaskList();
        list.Apply(new TaskListCreated(ListId, Owner, Now, Guid.NewGuid(), "Errands"));
        list.Apply(new TaskListShared(list.Id, Owner, Now, Token, "Łukasz"));
        list.Apply(new TaskListJoined(list.Id, Owner, Now, Member, "Anna"));
        return list;
    }
}
