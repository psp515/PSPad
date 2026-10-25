using PSPad.Abstractions;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Tasks.Tests.Lists.View;

static class ViewTasks
{
    public static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 10, 23);

    public static TodoTask Named(string name, DateTimeOffset? createdAt = null)
    {
        var task = new TodoTask();
        task.Apply(new TaskCreated(Guid.NewGuid(), User, createdAt ?? Now, Guid.NewGuid(), name));
        return task;
    }

    public static TodoTask Due(DateOnly day, string name = "Task") =>
        Run(Named(name), task => new SetTaskDueDate(Guid.NewGuid(), User, task.Id, day));

    public static TodoTask Undated(string name = "Task") => Named(name);

    public static TodoTask Recurring(RecurrenceRule rule, string name = "Task") =>
        Run(Named(name), task => new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, rule));

    public static TodoTask WithStep(TodoTask task, DateOnly? due, bool @checked = false)
    {
        var stepId = Guid.NewGuid();
        Run(task, t => new AddStep(Guid.NewGuid(), User, t.Id, stepId, "step"));
        if (due is not null) Run(task, t => new SetStepDueDate(Guid.NewGuid(), User, t.Id, stepId, due));
        if (@checked) Run(task, t => new CheckStep(Guid.NewGuid(), User, t.Id, stepId, true));
        return task;
    }

    public static TodoTask Prioritised(TodoTask task, Priority priority) =>
        Run(task, t => new SetTaskPriority(Guid.NewGuid(), User, t.Id, priority));

    public static TodoTask Starred(TodoTask task) =>
        Run(task, t => new StarTask(Guid.NewGuid(), User, t.Id, true));

    public static TodoTask Completed(TodoTask task) =>
        Run(task, t => new CompleteTask(Guid.NewGuid(), User, t.Id));

    public static TodoTask Ticked(TodoTask task, DateOnly day) =>
        Run(task, t => new CompleteOccurrence(Guid.NewGuid(), User, t.Id, day, true));

    public static TodoTask EndedOn(TodoTask task, DateOnly until) =>
        Run(task, t => new SetTaskDueDate(Guid.NewGuid(), User, t.Id, until));

    public static Goal GoalHolding(string name, params TodoTask[] tasks)
    {
        var goal = new Goal();
        goal.Apply(new GoalCreated(Guid.NewGuid(), User, Now, name));
        foreach (var task in tasks) goal.Apply(new TaskAddedToGoal(goal.Id, User, Now, task.Id, task.Name));
        return goal;
    }

    static TodoTask Run(TodoTask task, Func<TodoTask, ICommand> command)
    {
        task.ApplyAll(TodoTask.Decide(task, command(task), Now));
        return task;
    }
}
