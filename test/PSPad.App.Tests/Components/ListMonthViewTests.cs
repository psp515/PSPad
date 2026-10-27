using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using PSPad.App.Components;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class ListMonthViewTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly Guid List = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 10, 23);

    [Fact]
    public void ADayShowsTwoNamesAndCountsTheRest()
    {
        var day = new DateOnly(2026, 10, 27);
        var view = RenderMonth(tasks: [Due(day, "a"), Due(day, "b"), Due(day, "c")]);

        var cell = view.Find($"[data-day='{day:yyyy-MM-dd}']");
        Assert.Equal(2, cell.QuerySelectorAll(".pspad-month-entry").Length);
        Assert.Contains("+1 more", cell.TextContent);
    }

    [Fact]
    public void AnOverdueEntryIsMarked() =>
        Assert.Single(RenderMonth(tasks: [Due(new DateOnly(2026, 10, 20), "late")]).FindAll(".pspad-month-entry-overdue"));

    [Fact]
    public void ARecurringTaskShowsOnItsComingDays()
    {
        var mondays = Recurring(Named("Water the plants"), RecurrenceRule.Weekly(new DateOnly(2026, 9, 1), DayOfWeek.Monday));

        var view = RenderMonth(tasks: [mondays]);

        Assert.Empty(view.Find("[data-day='2026-10-19']").QuerySelectorAll(".pspad-month-entry"));
        Assert.Single(view.Find("[data-day='2026-10-26']").QuerySelectorAll(".pspad-month-entry"));
    }

    [Fact]
    public void TodayIsOutlinedAndSelectedByDefault()
    {
        var view = RenderMonth(tasks: [Due(Today, "now")]);

        Assert.Contains("pspad-month-day-today", view.Find($"[data-day='{Today:yyyy-MM-dd}']").ClassName);
        Assert.Contains("now", view.Find(".pspad-month-detail").TextContent);
    }

    [Fact]
    public void ClickingADayListsItsTasksAsRows()
    {
        var day = new DateOnly(2026, 10, 27);
        var view = RenderMonth(tasks: [Due(day, "a"), Due(day, "b")]);

        view.Find($"[data-day='{day:yyyy-MM-dd}']").Click();

        Assert.Equal(2, view.Find(".pspad-month-detail").QuerySelectorAll(".pspad-row").Length);
        Assert.Contains("pspad-month-day-selected", view.Find($"[data-day='{day:yyyy-MM-dd}']").ClassName);
    }

    [Fact]
    public void DayRowsCarryTheirGoal()
    {
        var task = Due(Today, "now");

        var view = RenderMonth(tasks: [task], goalNameOf: _ => "Move to Kraków");

        Assert.Contains("Move to Kraków", view.Find(".pspad-month-detail .pspad-row-goal").TextContent);
    }

    [Fact]
    public void EntriesInADayFollowTheSort()
    {
        var day = new DateOnly(2026, 10, 27);
        var view = RenderMonth(new ListSort(ListOrder.Name, true), [Due(day, "a"), Due(day, "b")]);

        var names = view.Find($"[data-day='{day:yyyy-MM-dd}']").QuerySelectorAll(".pspad-month-entry-name")
            .Select(entry => entry.TextContent.Trim());
        Assert.Equal(["b", "a"], names);
    }

    [Fact]
    public void TheUndatedLineLinksBack()
    {
        var shown = false;
        var view = RenderMonth(undated: 2, onShowUndated: () => shown = true);

        Assert.Contains("2 tasks have no date.", view.Find(".pspad-month-undated").TextContent);
        view.Find(".pspad-month-undated button").Click();

        Assert.True(shown);
    }

    [Fact]
    public void NoUndatedLineAtZero() => Assert.Empty(RenderMonth().FindAll(".pspad-month-undated"));

    [Fact]
    public void EveryDayCarriesAnAccessibleLabel() =>
        Assert.All(RenderMonth().FindAll(".pspad-month-day"),
            cell => Assert.False(string.IsNullOrEmpty(cell.GetAttribute("aria-label"))));

    [Fact]
    public void ACellCarriesBothTheNamesAndThePhoneDot()
    {
        var day = new DateOnly(2026, 10, 27);
        var cell = RenderMonth(tasks: [Due(day, "a")]).Find($"[data-day='{day:yyyy-MM-dd}']");

        Assert.NotNull(cell.QuerySelector(".pspad-month-dot"));
        Assert.NotNull(cell.QuerySelector(".pspad-month-entry"));
    }

    [Fact]
    public void TheGridCoversWholeWeeksOfTheMonth()
    {
        var days = RenderMonth().FindAll(".pspad-month-day");

        Assert.Equal(0, days.Count % 7);
        Assert.Contains(days, cell => cell.GetAttribute("data-day") == "2026-10-01");
        Assert.Contains(days, cell => cell.GetAttribute("data-day") == "2026-10-31");
    }

    IRenderedComponent<ContainerFragment> RenderMonth(
        ListSort? sort = null, TodoTask[]? tasks = null, int undated = 0, Action? onShowUndated = null,
        Func<TodoTask, string?>? goalNameOf = null)
    {
        AppTestHost.Arrange(this, User, Today);
        RenderFragment fragment = builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ListMonthView>(1);
            builder.AddAttribute(2, nameof(ListMonthView.Tasks), (IReadOnlyList<TodoTask>)(tasks ?? []));
            builder.AddAttribute(3, nameof(ListMonthView.Sort), sort ?? ListSort.Default);
            builder.AddAttribute(4, nameof(ListMonthView.Today), Today);
            builder.AddAttribute(5, nameof(ListMonthView.Undated), undated);
            builder.AddAttribute(6, nameof(ListMonthView.OnShowUndated),
                EventCallback.Factory.Create(this, () => onShowUndated?.Invoke()));
            builder.AddAttribute(7, nameof(ListMonthView.GoalNameOf), goalNameOf);
            builder.CloseComponent();
        };
        return Render(fragment);
    }

    static TodoTask Named(string name)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), List, name), DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask Due(DateOnly day, string name)
    {
        var task = Named(name);
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, day), DateTimeOffset.UnixEpoch));
        return task;
    }

    static TodoTask Recurring(TodoTask task, RecurrenceRule rule)
    {
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, rule), DateTimeOffset.UnixEpoch));
        return task;
    }
}
