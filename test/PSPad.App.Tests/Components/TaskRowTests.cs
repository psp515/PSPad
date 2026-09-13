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

        Assert.Contains("0 / 2", row.Markup);
    }

    [Fact]
    public void ATaskWithNoStepsShowsNoProgress()
    {
        Arrange();

        var row = Render(Task("Plain"));

        Assert.DoesNotContain("/", row.Markup.Replace("</", "").Replace("/>", ""));
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

    static void Add(TodoTask task, string name) =>
        task.ApplyAll(TodoTask.Decide(
            task, new AddStep(Guid.NewGuid(), User, task.Id, Guid.NewGuid(), name),
            DateTimeOffset.UnixEpoch));
}
