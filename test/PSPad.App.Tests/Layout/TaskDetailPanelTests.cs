using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.Abstractions;
using PSPad.App.Layout;
using PSPad.App.State.Viewport;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;
using System.Globalization;

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
    public void ADeletedTaskShowsAsMissing()
    {
        var task = NewTask("First dance");
        task.ApplyAll(TodoTask.Decide(
            task, new DeleteTask(Guid.NewGuid(), User, task.Id), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        Assert.Empty(panel.FindAll(".pspad-task-name-field"));
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
    public void AnOpenTaskShowsAMarkdownFieldWithItsDescription()
    {
        var task = NewTask("Buy milk");
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDescription(Guid.NewGuid(), User, task.Id, "2% please"), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        Assert.Contains("2% please", panel.Markup);
    }

    [Fact]
    public async Task SavingTheDescriptionSendsSetTaskDescription()
    {
        var task = NewTask("Buy milk");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));
        panel.Find(".pspad-markdown-input textarea").Input("Whole milk");
        panel.Find(".pspad-markdown-input textarea").Blur();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal("Whole milk", reloaded!.Description);
    }

    [Fact]
    public void StepsComeFirstThenThePropertyRowsThenTheDescriptionSection()
    {
        var task = NewTask("Buy milk");
        Step(task, "Check the fridge");
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDescription(Guid.NewGuid(), User, task.Id, "2% please"), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        var markup = panel.Markup;
        var steps = markup.IndexOf("pspad-steps", StringComparison.Ordinal);
        var rows = markup.IndexOf("pspad-property-row", StringComparison.Ordinal);
        var description = markup.IndexOf("pspad-markdown-view", StringComparison.Ordinal);
        Assert.True(steps >= 0 && steps < rows && rows < description);
        var section = panel.Find(".pspad-panel-section");
        Assert.Equal("Description", section.QuerySelector(".pspad-panel-section-title")!.TextContent.Trim());
        Assert.NotNull(section.QuerySelector(".pspad-markdown-view"));
    }

    [Fact]
    public void ASavedDescriptionShowsRendered()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));
        panel.Find(".pspad-markdown-input textarea").Input("**Whole** milk");
        panel.Find(".pspad-markdown-input textarea").Blur();

        panel.WaitForAssertion(() => Assert.Contains("<strong>Whole</strong>", panel.Markup));
        Assert.Empty(panel.FindAll(".pspad-markdown-input"));
    }

    [Fact]
    public void SwitchingTaskIdWhileEditingShowsTheOtherTasksDescriptionRatherThanTheDraft()
    {
        var taskA = NewTask("Task A");
        taskA.ApplyAll(TodoTask.Decide(
            taskA, new SetTaskDescription(Guid.NewGuid(), User, taskA.Id, "A description"),
            DateTimeOffset.UnixEpoch));
        var taskB = NewTask("Task B");
        taskB.ApplyAll(TodoTask.Decide(
            taskB, new SetTaskDescription(Guid.NewGuid(), User, taskB.Id, "B description"),
            DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, taskA, taskB);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)taskA.Id));
        panel.Find(".pspad-markdown-edit").Click();
        panel.Find("textarea").Input("Draft for A");

        panel.Render(parameters => parameters.Add(p => p.TaskId, (Guid?)taskB.Id));

        Assert.Contains("B description", panel.Markup);
        Assert.DoesNotContain("Draft for A", panel.Markup);
    }

    [Fact]
    public void ARejectedDescriptionSaveStaysInEditAndSurfacesTheRejection()
    {
        var task = NewTask("Buy milk");
        AppTestHost.Arrange(this, User, Today, task);
        Services.AddSingleton<ICommandHandler<SetTaskDescription>>(
            new RejectingHandler<SetTaskDescription>("Task no longer exists."));

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));
        panel.Find(".pspad-markdown-input textarea").Input("Whole milk");
        panel.Find(".pspad-markdown-input textarea").Blur();

        Assert.Equal("Whole milk", panel.Find(".pspad-markdown-input textarea").TextContent);
        var snackbar = Services.GetRequiredService<ISnackbar>();
        Assert.Contains(snackbar.ShownSnackbars, snack => snack.Message?.Contains("Task no longer exists.") == true);
    }

    [Fact]
    public void ANewTaskDraftShowsNoDescriptionField()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.NewInList, (Guid?)Guid.NewGuid()));

        Assert.Empty(panel.FindAll(".pspad-markdown-input"));
        Assert.Empty(panel.FindAll(".pspad-markdown-view"));
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
        var list = NewList(Guid.NewGuid(), "Zakupy");
        var listId = list.Id;
        var replica = AppTestHost.Arrange(this, User, Today, list);
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
    public async Task EnterAddsTheNewTask()
    {
        var list = NewList(Guid.NewGuid(), "Zakupy");
        var replica = AppTestHost.Arrange(this, User, Today, list);
        var closed = false;

        var panel = Render<TaskDetailPanel>(parameters => parameters
            .Add(p => p.NewInList, (Guid?)list.Id)
            .Add(p => p.OnClose, () => closed = true));
        panel.Find(".pspad-task-name-field input").Input("Kup chleb");
        panel.Find(".pspad-task-name-field input").KeyDown(
            new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        var created = Assert.Single(await replica.LoadAllAsync<TodoTask>(User));
        Assert.Equal("Kup chleb", created.Name);
        Assert.True(closed);
    }

    [Fact]
    public async Task EnterOnAnExistingTaskNameCreatesNothing()
    {
        var list = NewList(Guid.NewGuid(), "Zakupy");
        var task = NewTask("Buy milk", list.Id);
        var replica = AppTestHost.Arrange(this, User, Today, list, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));
        panel.Find(".pspad-task-name-field input").KeyDown(
            new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        Assert.Single(await replica.LoadAllAsync<TodoTask>(User));
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

        var order = new[] { "pspad-task-name-field", "pspad-steps", "pspad-task-kind", "pspad-task-due",
            "pspad-task-lead", "pspad-task-priority", "pspad-task-goal", "pspad-task-list" }
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
    public async Task PickADateOpensACalendarThatSetsTheDate()
    {
        var task = NewTask("Buy milk");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        OpenRow(panel, ".pspad-task-due");
        panel.Find(".pspad-due-pick").Click();

        var dialog = panel.WaitForElement(".pspad-due-dialog");
        Assert.DoesNotContain("d-none", dialog.ClassName);

        var targetMonth = new DateOnly(Today.Year, Today.Month, 1);

        while (true)
        {
            // TODO: Most XD test i have ever seen
            var header = dialog.QuerySelector(".mud-button-month p")!.TextContent.Trim();

            if (DateTime.TryParseExact(
                    header,
                    "MMMM yyyy",
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.None,
                    out var displayedMonth) &&
                displayedMonth.Year == targetMonth.Year &&
                displayedMonth.Month == targetMonth.Month)
            {
                break;
            }

            dialog.QuerySelector(".mud-picker-nav-button-prev")!.Click();
        }

        dialog.QuerySelectorAll("button.mud-day")
            .First(day => day.TextContent.Trim() == "20" && !day.ClassList.Contains("mud-hidden"))
            .Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal(new DateOnly(Today.Year, Today.Month, 20), reloaded!.DueOn);
        panel.WaitForAssertion(() => Assert.Empty(panel.FindAll(".pspad-due-dialog")));
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
    public async Task ACustomRepeatCanBeYearly()
    {
        var task = Recurring(NewTask("Car insurance"), RecurrenceRule.Daily(Today));
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        OpenRow(panel, ".pspad-task-repeat");
        panel.Find(".pspad-repeat-custom").Click();
        panel.WaitForElement(".pspad-repeat-dialog");
        panel.Find(".pspad-repeat-unit .mud-select-input").MouseDown();
        panel.FindAll(".mud-list-item").First(item => item.TextContent.Trim() == "years").Click();
        panel.Find(".pspad-repeat-save").Click();

        var rule = (await replica.LoadAsync<TodoTask>(task.Id))!.Recurrence!;
        Assert.Equal(RecurrenceKind.Yearly, rule.Kind);
        Assert.Equal(Today, rule.StartsOn);
    }

    [Fact]
    public async Task SwitchingToRepeatingThenPickingWeeklySetsTheRule()
    {
        var task = NewTask("Read a book");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        panel.FindAll(".pspad-task-kind .mud-toggle-item")[1].Click();
        OpenRow(panel, ".pspad-task-repeat");
        panel.FindAll(".pspad-repeat-option")[2].Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal(RecurrenceKind.Weekly, reloaded!.Recurrence!.Kind);
        panel.WaitForAssertion(() => Assert.Contains("Weekly on", panel.Find(".pspad-task-repeat").TextContent));
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
    public void TheListPickerOffersOnlyTaskLists()
    {
        var area = NewArea("Dom");
        var tasksList = NewList(area.Id, "Zakupy");
        var referenceList = NewReferenceList(area.Id, "Przepisy");
        var task = NewTask("Buy milk", tasksList.Id);
        AppTestHost.Arrange(this, User, Today, area, tasksList, referenceList, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        OpenRow(panel, ".pspad-task-list");

        var options = panel.FindAll(".pspad-list-option").Select(item => item.TextContent).ToArray();
        Assert.Contains("Zakupy", options);
        Assert.DoesNotContain("Przepisy", options);
    }

    [Fact]
    public async Task ANewTaskKeepsWhatItsMenusPicked()
    {
        var list = NewList(Guid.NewGuid(), "Zakupy");
        var listId = list.Id;
        var replica = AppTestHost.Arrange(this, User, Today, list);

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
    public void ANewTaskHasNoStepsOrListRowButCanRepeat()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.NewInList, (Guid?)Guid.NewGuid()));

        Assert.Empty(panel.FindAll(".pspad-steps"));
        Assert.Empty(panel.FindAll(".pspad-task-list"));
        panel.FindAll(".pspad-task-kind .mud-toggle-item")[1].Click();
        panel.Find(".pspad-task-repeat");
        Assert.Empty(panel.FindAll(".pspad-repeat-history"));
    }

    [Fact]
    public async Task ANewTaskKeepsItsRepeatAndUntilDate()
    {
        var list = NewList(Guid.NewGuid(), "Zakupy");
        var listId = list.Id;
        var replica = AppTestHost.Arrange(this, User, Today, list);

        var panel = RenderWithOverlays(newInList: listId);
        panel.Find(".pspad-task-name-field input").Input("Read a book");
        panel.FindAll(".pspad-task-kind .mud-toggle-item")[1].Click();
        panel.WaitForAssertion(() => Assert.Contains("Until", panel.Find(".pspad-task-due").TextContent));
        OpenRow(panel, ".pspad-task-due");
        panel.FindAll(".pspad-due-quick")[2].Click();
        panel.Find(".pspad-panel-save").Click();

        var created = Assert.Single(await replica.LoadAllAsync<TodoTask>(User));
        Assert.Equal(RecurrenceKind.Daily, created.Recurrence!.Kind);
        Assert.Equal(Today, created.Recurrence.StartsOn);
        Assert.Equal(Today.AddDays(2), created.DueOn);
    }

    [Fact]
    public void OnARecurringTaskTheDueRowReadsUntilAndStaysEnabled()
    {
        var task = Recurring(NewTask("Read a book"), RecurrenceRule.Daily(Today));
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        var due = panel.Find(".pspad-task-due");
        Assert.Contains("Until", due.TextContent);
        Assert.Contains("No end date", due.TextContent);
        Assert.False(due.QuerySelector(".pspad-property-activator")!.HasAttribute("disabled"));
    }

    [Fact]
    public async Task SettingUntilOnARecurringTaskSetsItsDueDate()
    {
        var task = Recurring(NewTask("Read a book"), RecurrenceRule.Daily(Today));
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        OpenRow(panel, ".pspad-task-due");
        panel.FindAll(".pspad-due-quick")[1].Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal(Today.AddDays(1), reloaded!.DueOn);
        Assert.True(reloaded.IsRecurring);
    }

    [Fact]
    public void TheRepeatRowTalliesDoneOccurrences()
    {
        var task = Recurring(NewTask("Read a book"), RecurrenceRule.Daily(Today.AddDays(-3)));
        foreach (var day in new[] { Today.AddDays(-2), Today.AddDays(-1) })
        {
            task.ApplyAll(TodoTask.Decide(
                task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, day, true), DateTimeOffset.UnixEpoch));
        }
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        Assert.Equal("Done 2 times · 2 in a row", panel.Find(".pspad-repeat-tally").TextContent.Trim());
    }

    [Fact]
    public async Task ACustomRepeatSavesItsIntervalWeekdaysAndStart()
    {
        var task = Recurring(NewTask("Water plants"), RecurrenceRule.Daily(Today));
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        OpenRow(panel, ".pspad-task-repeat");
        panel.Find(".pspad-repeat-custom").Click();
        var dialog = panel.WaitForElement(".pspad-repeat-dialog");
        Assert.DoesNotContain("d-none", dialog.ClassName);
        panel.Find(".pspad-repeat-every input").Change("3");
        panel.Find(".pspad-repeat-unit .mud-select-input").MouseDown();
        panel.WaitForAssertion(() => Assert.Contains(panel.FindAll(".mud-list-item"),
            item => item.TextContent.Trim() == "weeks"));
        panel.FindAll(".mud-list-item").First(item => item.TextContent.Trim() == "weeks").Click();
        panel.WaitForAssertion(() => Assert.Contains(panel.FindAll(".pspad-repeat-weekdays .mud-chip"),
            chip => chip.TextContent.Trim() == "Mon"));
        panel.FindAll(".pspad-repeat-weekdays .mud-chip").First(chip => chip.TextContent.Trim() == "Mon").Click();
        panel.Find(".pspad-repeat-save").Click();
        panel.WaitForAssertion(() => Assert.Contains("Every 3 weeks on Mon, Sat",
            panel.Find(".pspad-task-repeat").TextContent));

        var rule = (await replica.LoadAsync<TodoTask>(task.Id))!.Recurrence!;
        Assert.Equal(RecurrenceKind.Weekly, rule.Kind);
        Assert.Equal(3, rule.Interval);
        Assert.Equal(Today, rule.StartsOn);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Saturday], rule.Days.Order());
    }

    [Fact]
    public async Task ACustomRepeatEveryFewDaysOnANewTaskIsSentAfterCreating()
    {
        var list = NewList(Guid.NewGuid(), "Zakupy");
        var listId = list.Id;
        var replica = AppTestHost.Arrange(this, User, Today, list);

        var panel = RenderWithOverlays(newInList: listId);
        panel.Find(".pspad-task-name-field input").Input("Water plants");
        panel.FindAll(".pspad-task-kind .mud-toggle-item")[1].Click();
        OpenRow(panel, ".pspad-task-repeat");
        panel.Find(".pspad-repeat-custom").Click();
        panel.WaitForElement(".pspad-repeat-dialog");
        panel.Find(".pspad-repeat-every input").Change("2");
        panel.Find(".pspad-repeat-save").Click();
        panel.Find(".pspad-panel-save").Click();

        var created = Assert.Single(await replica.LoadAllAsync<TodoTask>(User));
        Assert.Equal(RecurrenceKind.Daily, created.Recurrence!.Kind);
        Assert.Equal(2, created.Recurrence.Interval);
    }

    [Fact]
    public async Task RepeatingAnOverdueTaskClearsItsDueDateRatherThanEndingIt()
    {
        var task = NewTask("Read a book");
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, Today.AddDays(-3)), DateTimeOffset.UnixEpoch));
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        panel.FindAll(".pspad-task-kind .mud-toggle-item")[1].Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.True(reloaded!.IsRecurring);
        Assert.Null(reloaded.DueOn);
        Assert.False(reloaded.EndedBy(Today));
    }

    [Fact]
    public async Task ANewTaskDropsADueDateBeforeItsRepeatStarts()
    {
        var list = NewList(Guid.NewGuid(), "Zakupy");
        var listId = list.Id;
        var replica = AppTestHost.Arrange(this, User, Today, list);

        var panel = RenderWithOverlays(newInList: listId);
        panel.Find(".pspad-task-name-field input").Input("Water plants");
        OpenRow(panel, ".pspad-task-due");
        panel.FindAll(".pspad-due-quick")[1].Click();
        panel.FindAll(".pspad-task-kind .mud-toggle-item")[1].Click();
        OpenRow(panel, ".pspad-task-repeat");
        panel.Find(".pspad-repeat-custom").Click();
        panel.WaitForElement(".pspad-repeat-dialog");
        await PickStartAsync(panel, Today.AddDays(5));
        panel.Find(".pspad-repeat-save").Click();
        panel.Find(".pspad-panel-save").Click();

        var created = Assert.Single(await replica.LoadAllAsync<TodoTask>(User));
        Assert.Equal(Today.AddDays(5), created.Recurrence!.StartsOn);
        Assert.Null(created.DueOn);
    }

    [Fact]
    public async Task MovingACustomRepeatsStartMovesItsUntouchedWeekday()
    {
        var task = Recurring(NewTask("Water plants"), RecurrenceRule.Daily(Today));
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        OpenRow(panel, ".pspad-task-repeat");
        panel.Find(".pspad-repeat-custom").Click();
        panel.WaitForElement(".pspad-repeat-dialog");
        panel.Find(".pspad-repeat-unit .mud-select-input").MouseDown();
        panel.FindAll(".mud-list-item").First(item => item.TextContent.Trim() == "weeks").Click();
        await PickStartAsync(panel, Today.AddDays(2));
        panel.Find(".pspad-repeat-save").Click();

        var rule = (await replica.LoadAsync<TodoTask>(task.Id))!.Recurrence!;
        Assert.Equal(Today.AddDays(2), rule.StartsOn);
        Assert.Equal([Today.AddDays(2).DayOfWeek], rule.Days);
    }

    [Fact]
    public async Task MovingACustomRepeatsStartMovesItsUntouchedDayOfMonth()
    {
        var task = Recurring(NewTask("Pay rent"), RecurrenceRule.Daily(Today));
        var replica = AppTestHost.Arrange(this, User, Today);
        await replica.SaveAsync(task);

        var panel = RenderWithOverlays(taskId: task.Id);
        OpenRow(panel, ".pspad-task-repeat");
        panel.Find(".pspad-repeat-custom").Click();
        panel.WaitForElement(".pspad-repeat-dialog");
        panel.Find(".pspad-repeat-unit .mud-select-input").MouseDown();
        panel.FindAll(".mud-list-item").First(item => item.TextContent.Trim() == "months").Click();
        await PickStartAsync(panel, Today.AddDays(3));
        panel.Find(".pspad-repeat-save").Click();

        var rule = (await replica.LoadAsync<TodoTask>(task.Id))!.Recurrence!;
        Assert.Equal(Today.AddDays(3).Day, rule.DayOfMonth);
    }

    [Fact]
    public void AOneTimeTaskShowsDueAndRemindButNoRepeat()
    {
        var task = NewTask("Renew passport");
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        Assert.Contains("One-time", panel.Find(".pspad-task-kind .mud-toggle-item-selected").TextContent);
        Assert.Contains("Due", panel.Find(".pspad-task-due").TextContent);
        Assert.Empty(panel.FindAll(".pspad-task-repeat"));
        Assert.Contains("1 week before", panel.Find(".pspad-task-lead").TextContent);
    }

    [Fact]
    public void ARepeatingTaskShowsRepeatUntilAndRemind()
    {
        var task = Recurring(NewTask("Read a book"), RecurrenceRule.Daily(Today));
        AppTestHost.Arrange(this, User, Today, task);

        var panel = Render<TaskDetailPanel>(parameters => parameters.Add(p => p.TaskId, (Guid?)task.Id));

        Assert.Contains("Repeating", panel.Find(".pspad-task-kind .mud-toggle-item-selected").TextContent);
        panel.Find(".pspad-task-repeat");
        Assert.Contains("Until", panel.Find(".pspad-task-due").TextContent);
        panel.Find(".pspad-task-lead");
    }

    [Fact]
    public async Task SwitchingToRepeatingRepeatsDailyFromTodayAndDropsTheDueDate()
    {
        var task = NewTask("Water plants");
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, Today.AddDays(3)), DateTimeOffset.UnixEpoch));
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        panel.FindAll(".pspad-task-kind .mud-toggle-item")[1].Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal(RecurrenceKind.Daily, reloaded!.Recurrence!.Kind);
        Assert.Equal(Today, reloaded.Recurrence.StartsOn);
        Assert.Null(reloaded.DueOn);
        panel.WaitForAssertion(() => panel.Find(".pspad-task-repeat"));
    }

    [Fact]
    public async Task SwitchingToOneTimeStopsTheRepeatAndDropsUntil()
    {
        var task = Recurring(NewTask("Read a book"), RecurrenceRule.Daily(Today));
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, Today.AddDays(30)), DateTimeOffset.UnixEpoch));
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        panel.FindAll(".pspad-task-kind .mud-toggle-item")[0].Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Null(reloaded!.Recurrence);
        Assert.Null(reloaded.DueOn);
        panel.WaitForAssertion(() => Assert.Empty(panel.FindAll(".pspad-task-repeat")));
    }

    [Fact]
    public async Task PickingALeadTimeSetsIt()
    {
        var task = NewTask("Renew passport");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        OpenRow(panel, ".pspad-task-lead");
        panel.FindAll(".pspad-lead-option")[1].Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal(new LeadTime(3, LeadUnit.Days), reloaded!.LeadTime);
        panel.WaitForAssertion(() => Assert.Contains("3 days before", panel.Find(".pspad-task-lead").TextContent));
    }

    [Fact]
    public async Task ClearingALeadTimeFallsBackToTheDefault()
    {
        var task = NewTask("Renew passport");
        task.ApplyAll(TodoTask.Decide(task, new SetTaskLeadTime(
            Guid.NewGuid(), User, task.Id, LeadTime.Of(1, LeadUnit.Months)), DateTimeOffset.UnixEpoch));
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        panel.Find(".pspad-task-lead .pspad-property-clear").Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Null(reloaded!.LeadTime);
    }

    [Fact]
    public async Task ACustomLeadTimeIsSaved()
    {
        var task = NewTask("Renew passport");
        var replica = AppTestHost.Arrange(this, User, Today, task);

        var panel = RenderWithOverlays(taskId: task.Id);
        OpenRow(panel, ".pspad-task-lead");
        panel.Find(".pspad-lead-custom").Click();
        panel.WaitForElement(".pspad-lead-dialog");
        panel.Find(".pspad-lead-amount input").Change("2");
        panel.Find(".pspad-lead-unit .mud-select-input").MouseDown();
        panel.FindAll(".mud-list-item").First(item => item.TextContent.Trim() == "months").Click();
        panel.Find(".pspad-lead-save").Click();

        var reloaded = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal(new LeadTime(2, LeadUnit.Months), reloaded!.LeadTime);
    }

    [Fact]
    public async Task ANewRepeatingTaskKeepsItsRuleUntilAndLeadTime()
    {
        var list = NewList(Guid.NewGuid(), "Bills");
        var replica = AppTestHost.Arrange(this, User, Today, list);

        var panel = RenderWithOverlays(newInList: list.Id);
        panel.Find(".pspad-task-name-field input").Input("Car insurance");
        panel.FindAll(".pspad-task-kind .mud-toggle-item")[1].Click();
        OpenRow(panel, ".pspad-task-repeat");
        panel.FindAll(".pspad-repeat-option")[4].Click();
        OpenRow(panel, ".pspad-task-lead");
        panel.FindAll(".pspad-lead-option")[4].Click();
        panel.Find(".pspad-panel-save").Click();

        var created = Assert.Single(await replica.LoadAllAsync<TodoTask>(User));
        Assert.Equal(RecurrenceKind.Yearly, created.Recurrence!.Kind);
        Assert.Equal(Today, created.Recurrence.StartsOn);
        Assert.Equal(new LeadTime(1, LeadUnit.Months), created.LeadTime);
    }

    [Fact]
    public void SwitchingANewTaskBackToOneTimeDropsItsDraftRepeat()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = RenderWithOverlays(newInList: Guid.NewGuid());
        panel.FindAll(".pspad-task-kind .mud-toggle-item")[1].Click();
        panel.WaitForAssertion(() => panel.Find(".pspad-task-repeat"));
        panel.FindAll(".pspad-task-kind .mud-toggle-item")[0].Click();

        panel.WaitForAssertion(() => Assert.Empty(panel.FindAll(".pspad-task-repeat")));
        Assert.Contains("Due", panel.Find(".pspad-task-due").TextContent);
    }

    static Task PickStartAsync(IRenderedComponent<ContainerFragment> panel, DateOnly start)
    {
        var picker = panel.FindComponents<MudDatePicker>()
            .Single(found => found.Instance.Class?.Contains("pspad-repeat-start") == true);
        return panel.InvokeAsync(() => picker.Instance.DateChanged.InvokeAsync(start.ToDateTime(TimeOnly.MinValue)));
    }

    static TodoTask Recurring(TodoTask task, RecurrenceRule rule)
    {
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, rule), DateTimeOffset.UnixEpoch));
        return task;
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

    [Fact]
    public void TheGoalMenuOffersOnlyGoalsInProgressButStillNamesALinkedClosedGoal()
    {
        var open = NewGoalWith("Zdrowie", GoalStatus.InProgress);
        var done = NewGoalWith("Maraton", GoalStatus.Achieved);
        var task = NewTask("Run");
        task.ApplyAll(TodoTask.Decide(task, new LinkTaskToGoal(Guid.NewGuid(), User, task.Id, done.Id), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, Today, task, open, done);

        var panel = RenderWithOverlays(taskId: task.Id);
        Assert.Contains("Maraton", panel.Find(".pspad-task-goal").TextContent);
        OpenRow(panel, ".pspad-task-goal");

        Assert.Equal(["Zdrowie"], panel.FindAll(".pspad-goal-option").Select(option => option.TextContent.Trim()));
    }

    static PSPad.Module.Tasks.Goals.Goal NewGoalWith(string name, PSPad.Module.Tasks.Goals.GoalStatus status)
    {
        var goal = new PSPad.Module.Tasks.Goals.Goal();
        goal.ApplyAll(PSPad.Module.Tasks.Goals.Goal.Decide(null,
            new PSPad.Module.Tasks.Goals.CreateGoal(Guid.NewGuid(), User, Guid.NewGuid(), name), DateTimeOffset.UnixEpoch));
        goal.ApplyAll(PSPad.Module.Tasks.Goals.Goal.Decide(goal,
            new PSPad.Module.Tasks.Goals.SetGoalStatus(Guid.NewGuid(), User, goal.Id, status), DateTimeOffset.UnixEpoch));
        return goal;
    }

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
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, name),
            DateTimeOffset.UnixEpoch));
        return list;
    }

    static TaskList NewReferenceList(Guid areaId, string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, name, ListKind.Reference),
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
