using Bunit;
using Microsoft.AspNetCore.Components.Web;
using PSPad.App.Components;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class GoalSummaryCardTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void AnAchievedGoalSumsUpItsTasks()
    {
        AppTestHost.Arrange(this, User, Today);
        var goal = NewGoal("Run a marathon", GoalStatus.Achieved);

        var card = Render<GoalSummaryCard>(parameters => parameters
            .Add(p => p.Goal, goal)
            
            .Add(p => p.Tasks, [NewTask("Buy shoes", done: true), NewTask("Train", done: true), NewTask("Rest")]));

        Assert.Contains("Run a marathon", card.Find(".pspad-goal-summary-name").TextContent);
        Assert.Contains("2 of 3 tasks done", card.Find(".pspad-goal-summary-tasks").TextContent);
        Assert.Contains("pspad-goal-summary-achieved", card.Find(".pspad-goal-summary").ClassName);
        Assert.Empty(card.FindAll(".mud-progress-linear"));
    }

    [Fact]
    public void ANotAchievedGoalWithNoTasksSaysSo()
    {
        AppTestHost.Arrange(this, User, Today);
        var goal = NewGoal("Learn Polish", GoalStatus.NotAchieved);

        var card = Render<GoalSummaryCard>(parameters => parameters.Add(p => p.Goal, goal));

        Assert.Contains("No tasks linked", card.Markup);
        Assert.Contains("pspad-goal-summary-not-achieved", card.Find(".pspad-goal-summary").ClassName);
    }

    [Fact]
    public void ASummaryLeavesTheDueDateOut()
    {
        AppTestHost.Arrange(this, User, Today);
        var goal = NewGoal("Learn Polish", GoalStatus.NotAchieved, Today.AddDays(1));

        var card = Render<GoalSummaryCard>(parameters => parameters.Add(p => p.Goal, goal));

        Assert.DoesNotContain("Due", card.Markup);
    }

    [Fact]
    public void ClickingOrPressingEnterOpensIt()
    {
        AppTestHost.Arrange(this, User, Today);
        var opened = 0;

        var card = Render<GoalSummaryCard>(parameters => parameters
            .Add(p => p.Goal, NewGoal("Learn Polish", GoalStatus.Achieved))
            .Add(p => p.OnOpen, () => opened++));
        card.Find(".pspad-goal-summary").Click();
        card.Find(".pspad-goal-summary").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(2, opened);
    }

    static Goal NewGoal(string name, GoalStatus status, DateOnly? dueOn = null)
    {
        var goal = new Goal();
        goal.ApplyAll(Goal.Decide(null, new CreateGoal(Guid.NewGuid(), User, Guid.NewGuid(), name), DateTimeOffset.UnixEpoch));
        goal.ApplyAll(Goal.Decide(goal, new SetGoalStatus(Guid.NewGuid(), User, goal.Id, status), DateTimeOffset.UnixEpoch));
        goal.ApplyAll(Goal.Decide(goal, new SetGoalDueDate(Guid.NewGuid(), User, goal.Id, dueOn), DateTimeOffset.UnixEpoch));
        return goal;
    }

    static TodoTask NewTask(string name, bool done = false)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name),
            DateTimeOffset.UnixEpoch));

        if (done)
        {
            task.ApplyAll(TodoTask.Decide(task, new CompleteTask(Guid.NewGuid(), User, task.Id), DateTimeOffset.UnixEpoch));
        }

        return task;
    }
}
