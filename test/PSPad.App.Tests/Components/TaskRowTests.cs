using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class TaskRowTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void ItShowsTheName()
    {
        Arrange();

        var row = Render(Task("Buy milk"));

        Assert.Contains("Buy milk", row.Markup);
    }

    [Fact]
    public void ATaskDueYesterdayIsOverdue()
    {
        Arrange();

        var row = Render(Due(Task("Buy paint"), Today.AddDays(-1)));

        Assert.Contains("overdue", row.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ARecurringTaskMissedLastWeekIsNotOverdue()
    {
        Arrange();

        var row = Render(Recurring(Task("Read a book"), Today.AddDays(-7)));

        Assert.DoesNotContain("overdue", row.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ACompletedTaskWithAPastDueDateIsNotOverdue()
    {
        Arrange();

        var row = Render(Complete(Due(Task("Buy paint"), Today.AddDays(-1))));

        Assert.DoesNotContain("overdue", row.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ATaskOverdueOnlyThroughAStepDueDateIsOverdue()
    {
        Arrange();
        var task = Task("Plan trip");
        Add(task, "Book flights");

        var row = Render(StepDue(task, Today.AddDays(-1)));

        Assert.Contains("overdue", row.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ACompletedNonRecurringTaskRendersStruckThroughAndChecked()
    {
        Arrange();

        var row = Render(Complete(Task("Buy milk")));

        Assert.Contains("text-decoration:line-through", row.Markup);
        Assert.True(row.Find("input.mud-checkbox-input").HasAttribute("checked"));
    }

    [Fact]
    public void ARecurringTaskCompletedTodayRendersStruckThroughAndChecked()
    {
        Arrange();

        var row = Render(CompleteOccurrenceOn(Recurring(Task("Read a book"), Today), Today));

        Assert.Contains("text-decoration:line-through", row.Markup);
        Assert.True(row.Find("input.mud-checkbox-input").HasAttribute("checked"));
    }

    [Fact]
    public void ARecurringTaskPastItsUntilDateRendersChecked()
    {
        Arrange();

        var row = Render(Due(Recurring(Task("Read a book"), Today.AddDays(-7)), Today.AddDays(-1)));

        Assert.True(row.Find("input.mud-checkbox-input").HasAttribute("checked"));
        Assert.Contains("Until", row.Markup);
    }

    [Fact]
    public void ARecurringTaskCarriesTheRecurrenceGlyph()
    {
        Arrange();

        var row = Render(Recurring(Task("Read a book"), Today));

        Assert.Contains("pspad-recurring", row.Markup);
    }

    [Fact]
    public void StepProgressShowsWhenTheTaskHasSteps()
    {
        Arrange();
        var task = Task("First dance");
        Add(task, "Find an idea");
        Add(task, "Get quotes");

        var row = Render(task);

        Assert.Equal("0/2", row.Find(".pspad-steps-progress").TextContent.Trim());
    }

    [Fact]
    public void ATaskWithNoStepsShowsNoProgress()
    {
        Arrange();

        var row = Render(Task("Plain"));

        Assert.DoesNotContain("/", row.Markup.Replace("</", "").Replace("/>", ""));
    }

    [Fact]
    public void ATaskWithADescriptionShowsTheNotesIcon()
    {
        Arrange();

        var row = Render(Described(Task("Buy milk"), "2% please"));

        Assert.Contains("pspad-has-description", row.Markup);
    }

    [Fact]
    public void ATaskWithNoDescriptionShowsNoNotesIcon()
    {
        Arrange();

        var row = Render(Task("Buy milk"));

        Assert.DoesNotContain("pspad-has-description", row.Markup);
    }

    [Fact]
    public void TheListNameShowsOnlyWhenGiven()
    {
        Arrange();

        var withList = Render(Task("Buy milk"), "Shopping");
        var without = Render(Task("Buy milk"));

        Assert.Contains("Shopping", withList.Markup);
        Assert.DoesNotContain("Shopping", without.Markup);
    }

    [Fact]
    public void ClickingTheNameRaisesOnOpen()
    {
        Arrange();
        var task = Task("Buy milk");
        TodoTask? opened = null;

        var row = Render<TaskRow>(parameters => parameters
            .Add(p => p.Task, task)
            .Add(p => p.Today, Today)
            .Add(p => p.OnOpen, t => opened = t));
        row.Find(".pspad-task-name").Click();

        Assert.Same(task, opened);
    }

    [Fact]
    public void TheNameIsOneLineWithTheFullNameOnHover()
    {
        Arrange();

        var row = Render(Task("Logi na USW1 do wyłączenia przy okazji"));

        var name = row.Find(".pspad-row-name");
        Assert.Equal("Logi na USW1 do wyłączenia przy okazji", name.GetAttribute("title"));
        Assert.Contains("pspad-row", row.Find("div").ClassList);
    }

    [Fact]
    public void ATaskWithNothingToShowHasNoMetaLine()
    {
        Arrange();

        var row = Render(Task("Skrypty upgradowe"));

        Assert.Empty(row.FindAll(".pspad-row-meta"));
    }

    [Fact]
    public void TheMetaLineRunsDueStepsRepeatDescriptionPriorityThenList()
    {
        Arrange();
        var task = Recurring(Task("Sprawdzaj Inwestycje"), Today);
        Add(task, "Check the fund");
        Described(task, "Quarterly");
        Prioritised(task, Priority.High);
        Due(task, Today.AddDays(5));

        var row = Render(task, "Regularne Zadania");

        var order = row.Find(".pspad-row-meta").Children
            .Select(child => child.ClassList.First(name => name != "mud-typography" && name.StartsWith("pspad-")))
            .ToArray();
        Assert.Equal(
            ["pspad-due", "pspad-steps-progress", "pspad-recurring", "pspad-has-description", "pspad-priority", "pspad-row-list"],
            order);
    }

    [Fact]
    public void ThePriorityDotSitsInTheMetaLineLeavingOnlyTheStarOnTheRight()
    {
        Arrange();

        var row = Render(Prioritised(Task("Scroodge"), Priority.Medium));

        row.Find(".pspad-row-meta .pspad-priority");
        Assert.Empty(row.FindAll(".pspad-row > .mud-icon-root"));
        row.Find(".pspad-row > .pspad-row-star");
    }

    [Fact]
    public void ATaskWithASnapshotMarkShowsTheMarkChip()
    {
        Arrange();

        var row = Render(Marked(Task("Buy milk")));

        Assert.Contains("pspad-snapshot-mark", row.Markup);
    }

    [Fact]
    public void ATaskWithoutASnapshotMarkShowsNoMarkChip()
    {
        Arrange();

        var row = Render(Task("Buy milk"));

        Assert.DoesNotContain("pspad-snapshot-mark", row.Markup);
    }

    [Fact]
    public void ATaskWithoutPriorityShowsNoDot()
    {
        Arrange();

        var row = Render(Task("Plain"));

        Assert.Empty(row.FindAll(".pspad-priority"));
    }

    [Fact]
    public void ATimedTaskShowsItsTimeFirst()
    {
        Arrange();

        var row = Render(Timed(Due(Task("Standup"), Today)));

        Assert.Equal("09:30–11:00", row.Find(".pspad-row-time").TextContent.Trim());
    }

    [Fact]
    public void AnOvernightTimeShowsItEndsTheNextDay()
    {
        Arrange();
        var task = Due(Task("Night shift"), Today);
        task.ApplyAll(TodoTask.Decide(
            task,
            new SetTaskTime(Guid.NewGuid(), User, task.Id, TaskTime.Of(new TimeOnly(22, 0), new TimeOnly(1, 0))),
            DateTimeOffset.UnixEpoch));

        var row = Render(task);

        Assert.Equal("22:00–01:00 (+1)", row.Find(".pspad-row-time").TextContent.Trim());
    }

    [Fact]
    public void AnUndatedTasksTimeIsNotShown()
    {
        Arrange();
        var task = Timed(Due(Task("Standup"), Today));
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, null), DateTimeOffset.UnixEpoch));

        var row = Render(task);

        Assert.Empty(row.FindAll(".pspad-row-time"));
    }

    [Fact]
    public void TheTimeCanBeHidden()
    {
        Arrange();

        var row = Render<TaskRow>(p => p
            .Add(r => r.Task, Timed(Due(Task("Standup"), Today)))
            .Add(r => r.Today, Today)
            .Add(r => r.ShowTime, false));

        Assert.Empty(row.FindAll(".pspad-row-time"));
    }

    static TodoTask Timed(TodoTask task)
    {
        task.ApplyAll(TodoTask.Decide(
            task,
            new SetTaskTime(Guid.NewGuid(), User, task.Id, TaskTime.Of(new TimeOnly(9, 30), new TimeOnly(11, 0))),
            DateTimeOffset.UnixEpoch));
        return task;
    }

    IRenderedComponent<TaskRow> Render(TodoTask task, string? listName = null) =>
        Render<TaskRow>(parameters => parameters
            .Add(p => p.Task, task)
            .Add(p => p.Today, Today)
            .Add(p => p.ListName, listName));

    void Arrange()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    static TodoTask Task(string name)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name),
            DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask Due(TodoTask task, DateOnly due)
    {
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, due), DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask Recurring(TodoTask task, DateOnly from)
    {
        task.ApplyAll(TodoTask.Decide(
            task,
            new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(from)),
            DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask Described(TodoTask task, string description)
    {
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDescription(Guid.NewGuid(), User, task.Id, description), DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask Prioritised(TodoTask task, Priority priority)
    {
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskPriority(Guid.NewGuid(), User, task.Id, priority), DateTimeOffset.UnixEpoch));
        return task;
    }

    static void Add(TodoTask task, string name) =>
        task.ApplyAll(TodoTask.Decide(
            task, new AddStep(Guid.NewGuid(), User, task.Id, Guid.NewGuid(), name),
            DateTimeOffset.UnixEpoch));

    static TodoTask Complete(TodoTask task)
    {
        task.ApplyAll(TodoTask.Decide(
            task, new CompleteTask(Guid.NewGuid(), User, task.Id), DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask CompleteOccurrenceOn(TodoTask task, DateOnly day)
    {
        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, day, true),
            DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask Marked(TodoTask task)
    {
        task.ApplyAll(TodoTask.Decide(
            task, new MarkTaskFromSnapshot(Guid.NewGuid(), User, task.Id, null, Guid.NewGuid(), true),
            DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask StepDue(TodoTask task, DateOnly due)
    {
        var stepId = task.Steps.Single().Id;
        task.ApplyAll(TodoTask.Decide(
            task, new SetStepDueDate(Guid.NewGuid(), User, task.Id, stepId, due),
            DateTimeOffset.UnixEpoch));
        return task;
    }
}
