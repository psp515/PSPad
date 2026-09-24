using Bunit;
using Bunit.Rendering;
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
    public void ANewTaskOffersAddButNoDeleteAndWaitsForAName()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.NewInList, (Guid?)Guid.NewGuid()));

        Assert.Contains("New task", panel.Markup);
        Assert.Empty(panel.FindAll(".pspad-task-delete"));
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
    public void InViewModeEveryControlIsDisabled()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters
            .Add(p => p.TaskId, (Guid?)task.Id)
            .Add(p => p.Disabled, true));

        Assert.True(panel.Find(".pspad-task-name-field input").HasAttribute("disabled"));
        Assert.True(panel.Find(".pspad-task-delete").HasAttribute("disabled"));
        Assert.True(panel.Find(".pspad-task-star").HasAttribute("disabled"));
    }

    [Fact]
    public void TheHeaderShowsWhereTheTaskLivesAndTheStar()
    {
        var area = NewArea("Dom");
        var list = NewList(area.Id, "Zakupy");
        var task = NewTask("Buy milk", list.Id);
        AppTestHost.Arrange(this, User, Today, area, list, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        Assert.Equal("Dom › Zakupy", panel.Find(".pspad-panel-title").TextContent.Trim());
        panel.Find(".pspad-task-star");
    }

    [Fact]
    public void TheNameIsALargeUnboxedFieldBesideTheDoneCheckbox()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        var field = panel.Find(".pspad-task-name-field");
        Assert.Equal("Buy milk", field.QuerySelector("input")!.GetAttribute("value"));
        Assert.Contains("mud-typography-h6", field.InnerHtml);
        Assert.DoesNotContain("mud-input-underline", field.InnerHtml);
        Assert.True(panel.Markup.IndexOf("pspad-task-done", StringComparison.Ordinal)
                    < panel.Markup.IndexOf("pspad-task-name-field", StringComparison.Ordinal));
    }

    [Fact]
    public void StepsFollowTheNameAndPropertyRowsFollowTheSteps()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        var order = new[] { "pspad-task-name-field", "pspad-steps", "pspad-task-due", "pspad-task-repeat",
            "pspad-task-priority", "pspad-task-goal", "pspad-task-list" }
            .Select(marker => panel.Markup.IndexOf(marker, StringComparison.Ordinal))
            .ToArray();
        Assert.DoesNotContain(-1, order);
        Assert.Equal(order.Order(), order);
    }

    [Fact]
    public void TheFooterCarriesTheCreatedDateAndADeleteIconButNoSave()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        var footer = panel.Find(".pspad-panel-footer");
        Assert.Contains("Created", footer.TextContent);
        Assert.NotNull(footer.QuerySelector(".pspad-task-delete"));
        Assert.Empty(panel.FindAll(".pspad-panel-save"));
    }

    [Fact]
    public async Task DeletingAsksFirstThenRemovesTheTask()
    {
        var task = NewTask("Buy milk");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        panel.Find(".pspad-task-delete").Click();
        panel.FindAll("div.mud-dialog button").Last().Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.True(reloaded!.Deleted);
    }

    [Fact]
    public async Task CancellingTheDeleteKeepsTheTask()
    {
        var task = NewTask("Buy milk");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        panel.Find(".pspad-task-delete").Click();
        panel.FindAll("div.mud-dialog button").First().Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.False(reloaded!.Deleted);
    }

    [Fact]
    public async Task AQuickPickFromTheDueMenuSetsTheDateStraightAway()
    {
        var task = NewTask("Buy milk");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        OpenRow(panel, ".pspad-task-due");
        panel.FindAll(".pspad-due-quick")[1].Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal(Today.AddDays(1), reloaded!.DueOn);
        panel.WaitForAssertion(() => Assert.Contains("Tomorrow", panel.Find(".pspad-task-due").TextContent));
    }

    [Fact]
    public async Task ClearingTheDueDateRemovesIt()
    {
        var task = NewTask("Buy milk");
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, Today), DateTimeOffset.UnixEpoch));
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));
        panel.Find(".pspad-task-due .pspad-property-clear").Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Null(reloaded!.DueOn);
    }

    [Fact]
    public async Task PickingAPriorityFromItsMenuAppliesIt()
    {
        var task = NewTask("Buy milk");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        OpenRow(panel, ".pspad-task-priority");
        panel.FindAll(".pspad-priority-option")[(int)Priority.High].Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal(Priority.High, reloaded!.Priority);
    }

    [Fact]
    public async Task PickingARepeatFromItsMenuMakesTheTaskRecurring()
    {
        var task = NewTask("Read a book");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        OpenRow(panel, ".pspad-task-repeat");
        panel.FindAll(".pspad-repeat-option")[0].Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.True(reloaded!.IsRecurring);
        panel.WaitForAssertion(() => Assert.Contains("Daily", panel.Find(".pspad-task-repeat").TextContent));
    }

    [Fact]
    public async Task PickingAListInAnotherAreaMovesTheTaskAtOnce()
    {
        var home = NewArea("Dom");
        var work = NewArea("Praca");
        var shopping = NewList(home.Id, "Zakupy");
        var office = NewList(work.Id, "Biuro");
        var task = NewTask("Buy milk", shopping.Id);
        var replica = AppTestHost.Arrange(this, User, Today, home, work, shopping, office, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        OpenRow(panel, ".pspad-task-list");
        panel.FindAll(".pspad-list-option").Single(item => item.TextContent.Contains("Biuro")).Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal(office.Id, reloaded!.ListId);
        panel.WaitForAssertion(() => Assert.Contains("Praca › Biuro", panel.Find(".pspad-panel-title").TextContent));
    }

    [Fact]
    public async Task ANewTaskKeepsWhatItsMenusPicked()
    {
        var listId = Guid.NewGuid();
        var replica = AppTestHost.Arrange(this, User, Today);

        var panel = RenderWithOverlays(newInList: listId);
        panel.Find(".pspad-task-name-field input").Input("Kup chleb");
        OpenRow(panel, ".pspad-task-due");
        panel.FindAll(".pspad-due-quick")[2].Click();
        OpenRow(panel, ".pspad-task-priority");
        panel.FindAll(".pspad-priority-option")[(int)Priority.Medium].Click();
        panel.Find(".pspad-panel-save").Click();

        var created = Assert.Single(await replica.LoadAllAsync<TodoTask>(User));
        Assert.Equal(Today.AddDays(2), created.DueOn);
        Assert.Equal(Priority.Medium, created.Priority);
    }

    [Fact]
    public void ANewTaskHasNoStepsRepeatOrListRow()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.NewInList, (Guid?)Guid.NewGuid()));

        Assert.Empty(panel.FindAll(".pspad-steps"));
        Assert.Empty(panel.FindAll(".pspad-task-repeat"));
        Assert.Empty(panel.FindAll(".pspad-task-list"));
    }

    IRenderedComponent<ContainerFragment> RenderWithOverlays(Guid? taskId = null, Guid? newInList = null) => Render(builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<TaskDetailPanel>(2);
        builder.AddAttribute(3, nameof(TaskDetailPanel.TaskId), taskId);
        builder.AddAttribute(4, nameof(TaskDetailPanel.NewInList), newInList);
        builder.CloseComponent();
    });

    static void OpenRow(IRenderedComponent<ContainerFragment> panel, string row) =>
        panel.Find($"{row} .pspad-property-activator").Click();

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
