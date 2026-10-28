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
        var view = RenderMonth(tasks: [Due(day, "Alpha"), Due(day, "Bravo"), Due(day, "Charlie")]);

        var cell = Cell(view, day).TextContent;
        Assert.Contains("Alpha", cell);
        Assert.Contains("Bravo", cell);
        Assert.DoesNotContain("Charlie", cell);
        Assert.Contains("+1 more", cell);
    }

    [Fact]
    public void ARecurringTaskShowsOnItsComingDays()
    {
        var mondays = Recurring(Named("Water the plants"), RecurrenceRule.Weekly(new DateOnly(2026, 9, 1), DayOfWeek.Monday));

        var view = RenderMonth(tasks: [mondays]);

        Assert.DoesNotContain("Water the plants", Cell(view, new DateOnly(2026, 10, 19)).TextContent);
        Assert.Contains("Water the plants", Cell(view, new DateOnly(2026, 10, 26)).TextContent);
    }

    [Fact]
    public void TodayIsSelectedByDefaultAndItsTasksAreListed()
    {
        var view = RenderMonth(tasks: [Due(Today, "Alpha")]);

        Assert.Equal("true", Cell(view, Today).GetAttribute("aria-pressed"));
        Assert.Contains("Alpha", view.Find("section").TextContent);
    }

    [Fact]
    public void ClickingADayListsItsTasksBelow()
    {
        var day = new DateOnly(2026, 10, 27);
        var view = RenderMonth(tasks: [Due(day, "Alpha"), Due(day, "Bravo")]);

        Cell(view, day).Click();

        var detail = view.Find("section").TextContent;
        Assert.Contains("Alpha", detail);
        Assert.Contains("Bravo", detail);
        Assert.Equal("true", Cell(view, day).GetAttribute("aria-pressed"));
        Assert.Equal("false", Cell(view, Today).GetAttribute("aria-pressed"));
    }

    [Fact]
    public void DayRowsCarryTheirGoal()
    {
        var task = Due(Today, "Alpha");

        var view = RenderMonth(tasks: [task], goalNameOf: _ => "Move to Kraków");

        Assert.Contains("Move to Kraków", view.Find("section").TextContent);
    }

    [Fact]
    public void EntriesInADayFollowTheSort()
    {
        var day = new DateOnly(2026, 10, 27);
        var view = RenderMonth(new ListSort(ListOrder.Name, true), [Due(day, "Alpha"), Due(day, "Bravo")]);

        var cell = Cell(view, day).TextContent;
        Assert.True(cell.IndexOf("Bravo", StringComparison.Ordinal) < cell.IndexOf("Alpha", StringComparison.Ordinal));
    }

    [Fact]
    public void TheUndatedLineLinksBack()
    {
        var shown = false;
        var view = RenderMonth(undated: 2, onShowUndated: () => shown = true);

        Assert.Contains("2 tasks have no date.", view.Text());
        view.FindAll("button").Single(button => button.TextContent.Contains("Show them")).Click();

        Assert.True(shown);
    }

    [Fact]
    public void NoUndatedLineAtZero() => Assert.DoesNotContain("have no date", RenderMonth().Text());

    [Fact]
    public void TheGridCoversWholeWeeksOfTheMonthAndLabelsEveryDay()
    {
        var days = DayCells(RenderMonth());

        Assert.Equal(0, days.Count % 7);
        Assert.All(days, cell => Assert.False(string.IsNullOrEmpty(cell.GetAttribute("aria-label"))));
        Assert.Contains(days, cell => cell.GetAttribute("aria-label")!.Contains(new DateOnly(2026, 10, 1).ToString("d MMMM")));
        Assert.Contains(days, cell => cell.GetAttribute("aria-label")!.Contains(new DateOnly(2026, 10, 31).ToString("d MMMM")));
    }

    static IReadOnlyList<AngleSharp.Dom.IElement> DayCells(IRenderedComponent<IComponent> view) =>
        [.. view.FindAll("button[aria-pressed]").Where(cell =>
            System.Text.RegularExpressions.Regex.IsMatch(cell.GetAttribute("aria-label")!, @", \d+ tasks?$"))];

    static AngleSharp.Dom.IElement Cell(IRenderedComponent<IComponent> view, DateOnly day) =>
        DayCells(view).First(cell => cell.GetAttribute("aria-label")!.Contains(day.ToString("d MMMM")));

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
