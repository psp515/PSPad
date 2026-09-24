using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.App.Layout;
using PSPad.App.State.Viewport;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
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
        Assert.Contains("Delete", panel.Find(".pspad-task-delete").TextContent);
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
        panel.Find(".pspad-task-delete").Click();

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
        Assert.Empty(panel.FindAll(".pspad-task-delete"));
        Assert.Empty(panel.FindAll(".pspad-task-actions"));
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
        panel.Find(".pspad-task-star").Click();
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

    [Fact]
    public void TheHeaderCarriesALargeTitleAndTheStar()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        var title = panel.Find(".pspad-panel-title");
        Assert.Contains("Edit task", title.TextContent);
        Assert.Contains("mud-typography-h5", title.ClassName);
        var markup = panel.Markup;
        Assert.True(markup.IndexOf("pspad-task-star", StringComparison.Ordinal)
                    < markup.IndexOf("pspad-task-name-field", StringComparison.Ordinal));
    }

    [Fact]
    public void TheNameIsALabelledFieldUnderTheHeader()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        Assert.Contains("Name", panel.Find(".pspad-task-name-field label").TextContent);
        Assert.Equal("Buy milk", panel.Find(".pspad-task-name-field input").GetAttribute("value"));
    }

    [Fact]
    public void DatePriorityAndGoalComeFirstAndActionsComeLast()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        var markup = panel.Markup;
        var due = markup.IndexOf("pspad-task-due", StringComparison.Ordinal);
        var priority = markup.IndexOf("pspad-task-priority", StringComparison.Ordinal);
        var goal = markup.IndexOf("pspad-task-goal", StringComparison.Ordinal);
        var actions = markup.IndexOf("pspad-task-actions", StringComparison.Ordinal);
        Assert.True(due < priority && priority < goal && goal < actions);
        Assert.Contains("Actions", panel.Find(".pspad-task-actions").TextContent);
    }

    [Fact]
    public void MoveSitsLeftOfDeleteAndBothAreFilled()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        var markup = panel.Markup;
        Assert.True(markup.IndexOf("pspad-task-move", StringComparison.Ordinal)
                    < markup.IndexOf("pspad-task-delete", StringComparison.Ordinal));
        Assert.Contains("mud-button-filled", panel.Find(".pspad-task-move").ClassName);
        Assert.Contains("mud-button-filled-error", panel.Find(".pspad-task-delete").ClassName);
    }

    [Fact]
    public void MoveStaysGreyWhileTheAreaAndListAreUnchanged()
    {
        var area = NewArea("Dom");
        var list = NewList(area.Id, "Zakupy");
        var task = NewTask("Buy milk", list.Id);
        AppTestHost.Arrange(this, User, Today, area, list, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        Assert.True(panel.Find(".pspad-task-move").HasAttribute("disabled"));
    }

    [Fact]
    public async Task PickingAnotherAreaAndListEnablesMoveWhichMovesTheTask()
    {
        var home = NewArea("Dom");
        var work = NewArea("Praca");
        var shopping = NewList(home.Id, "Zakupy");
        var office = NewList(work.Id, "Biuro");
        var task = NewTask("Buy milk", shopping.Id);
        var replica = AppTestHost.Arrange(this, User, Today, home, work, shopping, office, task);

        var panel = Render(WithPopovers(task.Id));
        panel.Find(".pspad-task-area").MouseDown();
        panel.FindAll(".mud-list-item").Single(item => item.TextContent.Contains("Praca")).Click();

        var move = panel.Find(".pspad-task-move");
        Assert.False(move.HasAttribute("disabled"));
        move.Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal(office.Id, reloaded!.ListId);
        panel.WaitForAssertion(() => Assert.True(panel.Find(".pspad-task-move").HasAttribute("disabled")));
    }

    [Fact]
    public void InViewModeEveryControlIsDisabled()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters
            .Add(p => p.TaskId, (Guid?)task.Id)
            .Add(p => p.Disabled, true));

        Assert.Contains("Task", panel.Find(".pspad-panel-title").TextContent);
        Assert.True(panel.Find(".pspad-task-name-field input").HasAttribute("disabled"));
        Assert.True(panel.Find(".pspad-task-delete").HasAttribute("disabled"));
        Assert.True(panel.Find(".pspad-task-star").HasAttribute("disabled"));
    }

    RenderFragment WithPopovers(Guid taskId) => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<TaskDetailPanel>(1);
        builder.AddAttribute(2, nameof(TaskDetailPanel.TaskId), (Guid?)taskId);
        builder.CloseComponent();
    };

    static Area NewArea(string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, 0), DateTimeOffset.UnixEpoch));
        return area;
    }

    static TaskList NewList(Guid areaId, string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, name, 0),
            DateTimeOffset.UnixEpoch));
        return list;
    }

    static TodoTask NewTask(string name, Guid? listId = null)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), listId ?? Guid.NewGuid(), name),
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
