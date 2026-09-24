using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.App.Layout;
using PSPad.App.State.Viewport;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class TaskDetailPanelTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void WithNoTaskItShowsNothing()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)null));

        Assert.DoesNotContain("First dance", panel.Markup);
    }

    [Fact]
    public void WithATaskItNamesItAndListsItsSteps()
    {
        var task = NewTask("First dance");
        Step(task, "Find an idea");
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        Assert.Contains("First dance", panel.Markup);
        Assert.Contains("Find an idea", panel.Markup);
    }

    [Fact]
    public void ClosingInvokesOnClose()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);
        var closed = false;

        var panel = Render<TaskDetailPanel>(parameters => parameters
            .Add(p => p.TaskId, (Guid?)task.Id)
            .Add(p => p.OnClose, () => closed = true));

        panel.Find(".pspad-panel-close").Click();

        Assert.True(closed);
    }

    [Fact]
    public async Task CompletingATaskWhileDisabledDoesNotChangeItsState()
    {
        var task = NewTask("Buy milk");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters
            .Add(p => p.TaskId, (Guid?)task.Id)
            .Add(p => p.Disabled, true));
        panel.Find(".pspad-task-done input").Change(true);

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Null(reloaded!.CompletedAt);
    }

    [Fact]
    public async Task CompletingARecurringTaskTicksTodaysOccurrenceRatherThanTheWholeTask()
    {
        var task = NewTask("Read a book");
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(Today)),
            DateTimeOffset.UnixEpoch));
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));
        panel.Find(".pspad-task-done input").Change(true);

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Contains(Today, reloaded!.CompletedDays);
        Assert.Null(reloaded.CompletedAt);
    }

    [Fact]
    public void ARecurringTaskShowsAMissedDayAsSkippedRatherThanOverdue()
    {
        var task = NewTask("Read a book");
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(Today.AddDays(-3))),
            DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        Assert.Contains("Skipped", panel.Markup);
        Assert.DoesNotContain("Overdue", panel.Markup);
    }

    [Fact]
    public void StepsRenderInPositionOrderRatherThanInsertionOrder()
    {
        var task = NewTask("Wedding prep");
        Step(task, "first");
        var secondId = Step(task, "second");
        task.ApplyAll(TodoTask.Decide(
            task, new MoveStep(Guid.NewGuid(), User, task.Id, secondId, 0),
            DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        var secondIndex = panel.Markup.IndexOf("second", StringComparison.Ordinal);
        var firstIndex = panel.Markup.IndexOf("first", StringComparison.Ordinal);
        Assert.True(secondIndex < firstIndex);
    }

    [Fact]
    public void OnADesktopViewportTheDrawerKeepsItsFixedColumnWidth()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        Assert.Contains("360px", panel.Find(".mud-drawer").GetAttribute("style"));
    }

    [Fact]
    public void OnASmallViewportTheDrawerFillsTheFullWidthInsteadOfAFixedColumn()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);
        Services.AddSingleton<IViewport>(new AppTestHost.FakeViewport(isDesktop: false));

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        Assert.Contains("100%", panel.Find(".mud-drawer").GetAttribute("style"));
    }

    [Fact]
    public void AnExistingTaskSavesAsItIsEditedSoItOffersDeleteButNoSave()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        Assert.Empty(panel.FindAll(".pspad-panel-save"));
        Assert.Contains("Delete task", panel.Find(".pspad-panel-delete").TextContent);
    }

    [Fact]
    public async Task EditingTheNameRenamesTheTaskStraightAway()
    {
        var task = NewTask("Buy milk");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));
        panel.Find(".pspad-task-name-field input").Change("Buy oat milk");

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal("Buy oat milk", reloaded!.Name);
    }

    [Fact]
    public async Task DeletingRemovesTheTaskAndCloses()
    {
        var task = NewTask("Buy milk");
        var replica = AppTestHost.Arrange(this, User, Today, task);
        var closed = false;

        var panel = Render<TaskDetailPanel>(parameters => parameters
            .Add(p => p.TaskId, (Guid?)task.Id)
            .Add(p => p.OnClose, () => closed = true));
        panel.Find(".pspad-panel-delete").Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.True(reloaded!.Deleted);
        Assert.True(closed);
    }

    [Fact]
    public void ANewTaskOffersAddButNoDeleteAndWaitsForAName()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.NewInList, (Guid?)Guid.NewGuid()));

        Assert.Contains("New task", panel.Markup);
        Assert.Empty(panel.FindAll(".pspad-panel-delete"));
        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));
    }

    [Fact]
    public async Task AddingANewTaskCreatesItInTheListAndCloses()
    {
        var listId = Guid.NewGuid();
        var replica = AppTestHost.Arrange(this, User, Today);
        var closed = false;

        var panel = Render<TaskDetailPanel>(parameters => parameters
            .Add(p => p.NewInList, (Guid?)listId)
            .Add(p => p.OnClose, () => closed = true));
        panel.Find(".pspad-task-name-field input").Input("Kup chleb");
        panel.Find(".pspad-new-task-star").Click();
        panel.Find(".pspad-panel-save").Click();

        var tasks = await replica.LoadAllAsync<TodoTask>(User);
        var created = Assert.Single(tasks);
        Assert.Equal(listId, created.ListId);
        Assert.Equal("Kup chleb", created.Name);
        Assert.True(created.Starred);
        Assert.True(closed);
    }

    [Fact]
    public async Task ANewTaskIsNotCreatedWhileDisabled()
    {
        var replica = AppTestHost.Arrange(this, User, Today);

        var panel = Render<TaskDetailPanel>(parameters => parameters
            .Add(p => p.NewInList, (Guid?)Guid.NewGuid())
            .Add(p => p.Disabled, true));
        panel.Find(".pspad-task-name-field input").Input("Kup chleb");

        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));
        Assert.Empty(await replica.LoadAllAsync<TodoTask>(User));
    }

    static TodoTask NewTask(string name)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name),
            DateTimeOffset.UnixEpoch));
        return task;
    }

    static Guid Step(TodoTask task, string name)
    {
        var stepId = Guid.NewGuid();
        task.ApplyAll(TodoTask.Decide(
            task, new AddStep(Guid.NewGuid(), User, task.Id, stepId, name),
            DateTimeOffset.UnixEpoch));
        return stepId;
    }
}
