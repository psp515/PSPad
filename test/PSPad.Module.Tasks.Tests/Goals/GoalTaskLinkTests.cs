using PSPad.Abstractions;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Tests.Fakes;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Goals;

[UnitTest]
public class GoalTaskLinkTests
{
    static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Member = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly Guid Stranger = Guid.Parse("33333333-3333-3333-3333-333333333333");
    static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
    const string Token = "k3Jv9s2mQ0x7b1nR4tYw8eZa";

    readonly FakeDocumentStore<Goal> _goals = new();
    readonly FakeDocumentStore<TodoTask> _tasks = new();
    readonly FakeDocumentStore<TaskList> _lists = new();
    readonly FakeUnitOfWork _work = new();

    [Fact]
    public async Task AddingATaskPutsItInTheGoalWithTheTaskName()
    {
        var goal = Seed(NewGoal(Owner, "Read more"));
        var task = Seed(NewTask(Owner, Seed(NewList(Owner)).Id, "Dune"));

        var result = await Add(Owner, goal.Id, task.Id);

        Assert.True(result.Accepted, result.Rejection);
        var added = Assert.IsType<TaskAddedToGoal>(Assert.Single(_work.Events));
        Assert.Equal((task.Id, "Dune", Owner), (added.TaskId, added.TaskName, added.UserId));
        Assert.Contains(task.Id, goal.TaskIds);
    }

    [Fact]
    public async Task AddingATaskAlreadyInTheGoalEmitsNothing()
    {
        var goal = Seed(NewGoal(Owner, "Read more"));
        var task = Seed(NewTask(Owner, Seed(NewList(Owner)).Id, "Dune"));
        await Add(Owner, goal.Id, task.Id);
        _work.Staged.Clear();

        await Add(Owner, goal.Id, task.Id);

        Assert.Empty(_work.Events);
    }

    [Fact]
    public async Task ATaskIsInAtMostOneOfAUsersGoals()
    {
        var first = Seed(NewGoal(Owner, "Read more"));
        var second = Seed(NewGoal(Owner, "Learn Polish"));
        var task = Seed(NewTask(Owner, Seed(NewList(Owner)).Id, "Dune"));
        await Add(Owner, first.Id, task.Id);
        _work.Staged.Clear();

        await Add(Owner, second.Id, task.Id);

        Assert.DoesNotContain(task.Id, first.TaskIds);
        Assert.Contains(task.Id, second.TaskIds);
        var removed = Assert.Single(_work.Events.OfType<TaskRemovedFromGoal>());
        Assert.Equal(second.Id, removed.MovedTo);
    }

    [Fact]
    public async Task SomeoneElsesGoalIsRejected()
    {
        var goal = Seed(NewGoal(Owner, "Private plans"));
        var task = Seed(NewTask(Stranger, Seed(NewList(Stranger)).Id, "Mine"));

        var result = await Add(Stranger, goal.Id, task.Id);

        Assert.False(result.Accepted);
        Assert.Equal("That goal belongs to somebody else.", result.Rejection);
        Assert.False(_work.Committed);
    }

    [Fact]
    public async Task ATaskInAListTheUserCannotSeeIsRejected()
    {
        var goal = Seed(NewGoal(Stranger, "Snooping"));
        var task = Seed(NewTask(Owner, Seed(NewList(Owner)).Id, "Private"));

        var result = await Add(Stranger, goal.Id, task.Id);

        Assert.False(result.Accepted);
        Assert.False(_work.Committed);
    }

    [Fact]
    public async Task AMemberPutsASharedTaskInTheirOwnGoal()
    {
        var list = Seed(SharedWith(NewList(Owner), Member));
        var task = Seed(NewTask(Owner, list.Id, "Dune"));
        var membersGoal = Seed(NewGoal(Member, "Read more"));

        var result = await Add(Member, membersGoal.Id, task.Id);

        Assert.True(result.Accepted, result.Rejection);
        Assert.Contains(task.Id, membersGoal.TaskIds);
    }

    [Fact]
    public async Task RemovingTakesTheTaskOutAndRemovingAgainEmitsNothing()
    {
        var goal = Seed(NewGoal(Owner, "Read more"));
        var task = Seed(NewTask(Owner, Seed(NewList(Owner)).Id, "Dune"));
        await Add(Owner, goal.Id, task.Id);

        await Remove(Owner, goal.Id, task.Id);
        Assert.DoesNotContain(task.Id, goal.TaskIds);
        _work.Staged.Clear();

        await Remove(Owner, goal.Id, task.Id);
        Assert.Empty(_work.Events);
    }

    Task<CommandResult> Add(Guid user, Guid goalId, Guid taskId) =>
        new AddTaskToGoalHandler(_goals, _tasks, _lists, _work, new FixedClock(Now))
            .HandleAsync(new AddTaskToGoal(Guid.NewGuid(), user, goalId, taskId), CancellationToken.None);

    Task<CommandResult> Remove(Guid user, Guid goalId, Guid taskId) =>
        new RemoveTaskFromGoalHandler(_goals, _tasks, _work, new FixedClock(Now))
            .HandleAsync(new RemoveTaskFromGoal(Guid.NewGuid(), user, goalId, taskId), CancellationToken.None);

    T Seed<T>(T document) where T : Aggregate
    {
        switch (document)
        {
            case Goal goal: _goals.Seed(goal); break;
            case TodoTask task: _tasks.Seed(task); break;
            case TaskList list: _lists.Seed(list); break;
        }

        return document;
    }

    static Goal NewGoal(Guid user, string name)
    {
        var goal = new Goal();
        goal.ApplyAll(Goal.Decide(null, new CreateGoal(Guid.NewGuid(), user, Guid.NewGuid(), name), Now));
        return goal;
    }

    static TaskList NewList(Guid user)
    {
        var list = new TaskList();
        list.Apply(new TaskListCreated(Guid.NewGuid(), user, Now, Guid.NewGuid(), "Books"));
        return list;
    }

    static TaskList SharedWith(TaskList list, Guid member)
    {
        list.Apply(new TaskListShared(list.Id, list.UserId, Now, Token, "Owner", "K7M4PX"));
        list.Apply(new TaskListJoined(list.Id, list.UserId, Now, member, "Member"));
        return list;
    }

    static TodoTask NewTask(Guid user, Guid listId, string name)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(null, new CreateTask(Guid.NewGuid(), user, Guid.NewGuid(), listId, name), Now));
        return task;
    }
}
