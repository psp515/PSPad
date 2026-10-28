using Bunit;
using PSPad.App.Components;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class DayTimelineTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Day = new(2026, 9, 12);

    [Fact]
    public void EmptyStretchesFoldIntoBandsAndBusyHoursStay()
    {
        var timeline = Render(Timed("Sprint planning", 9, 30, 11, 30));

        Assert.Equal(["Show 00:00–09:00", "Show 12:00–24:00"], Folds(timeline).Select(fold => fold.GetAttribute("aria-label")));
        Assert.Contains("10:00", timeline.Text());
        Assert.Contains("11:00", timeline.Text());
        Assert.DoesNotContain("13:00", timeline.Text());
    }

    [Fact]
    public void AnEmptyDayIsOneBand()
    {
        var timeline = Render();

        Assert.Equal("00:00–24:00", Assert.Single(Folds(timeline)).TextContent.Trim());
        Assert.DoesNotContain("10:00", timeline.Text().Replace("00:00–24:00", ""));
    }

    [Fact]
    public void ClickingABandShowsItsHours()
    {
        var timeline = Render(Timed("Sprint planning", 9, 30, 11, 30));
        Assert.DoesNotContain("05:00", timeline.Text());

        Folds(timeline)[0].Click();

        Assert.Contains("05:00", timeline.Text());
        Assert.Single(Folds(timeline));
    }

    [Fact]
    public void ABlockShowsItsNameTimesAndList()
    {
        var timeline = Render(Timed("Sprint planning", 9, 30, 11, 30));

        var block = timeline.Find("[title='Sprint planning – 09:30–11:30']");
        Assert.Contains("Sprint planning", block.TextContent);
        Assert.Contains("09:30", block.TextContent);
        Assert.Contains("11:30", block.TextContent);
        Assert.Contains("Praca", block.TextContent);
    }

    [Fact]
    public void ABlockUnderTwoHoursLeavesOutItsList()
    {
        var timeline = Render(Timed("Review", 9, 0, 10, 30), Timed("Call", 12, 0, 13, 15));

        Assert.DoesNotContain("Praca", timeline.Find("[title='Review – 09:00–10:30']").TextContent);
        Assert.DoesNotContain("Praca", timeline.Find("[title='Call – 12:00–13:15']").TextContent);
    }

    [Fact]
    public void ATaskWithoutAnEndShowsOnlyItsStart()
    {
        var timeline = Render(Timed("Standup", 9, 30, null, null));

        var block = timeline.Find("[title='Standup – 09:30']");
        Assert.DoesNotContain("–", block.TextContent);
    }

    [Fact]
    public void AnOvernightTaskIsMarkedAsEndingTheNextDay()
    {
        var timeline = Render(Timed("Night shift", 22, 0, 1, 0));

        var block = timeline.Find("[title='Night shift – 22:00–01:00 (+1)']");
        Assert.Contains("(+1)", block.TextContent);
    }

    [Fact]
    public void ThreeOverlappingTasksBecomeOneGroupOfLines()
    {
        var timeline = Render(Timed("Standup", 9, 0, 9, 15), Timed("Call", 9, 0, 9, 30), Timed("Review", 9, 0, 10, 0));

        Assert.Equal(["Call", "Review", "Standup"], timeline.FindAll("[title]").Select(line => line.GetAttribute("title")));
        var call = timeline.Find("[title='Call']").ParentElement!.TextContent;
        Assert.Contains("09:00", call);
        Assert.Contains("09:30", call);
    }

    [Fact]
    public void ALongTaskStaysABlockWhileTheTasksBesideItAreGrouped()
    {
        var timeline = Render(Timed("Workday", 10, 0, 18, 0), Timed("Call", 11, 0, null, null),
            Timed("Review", 16, 0, 17, 0), Timed("Sync", 16, 15, 17, 0), Timed("Demo", 16, 45, 17, 15));

        Assert.Equal(
            ["Workday – 10:00–18:00", "Call – 11:00", "Review", "Sync", "Demo"],
            timeline.FindAll("[title]").Select(item => item.GetAttribute("title")));
    }

    [Fact]
    public void TheNowLabelShowsOnlyWhenNowIsGiven()
    {
        var without = Render(Timed("A", 9, 0, 10, 0));
        Assert.DoesNotContain("09:15", without.Text());

        var with = Render([Timed("A", 9, 0, 10, 0)], new TimeOnly(9, 15));
        Assert.Contains("09:15", with.Text());
    }

    [Fact]
    public void ClickingAnEmptyHalfHourAsksForATaskThere()
    {
        TimeOnly? asked = null;
        var timeline = Render<DayTimeline>(parameters =>
            Bind(parameters, [Timed("A", 9, 0, 10, 0), Timed("B", 11, 0, 12, 0)], null).Add(p => p.OnAdd, time => asked = time));

        timeline.Find("button[aria-label='Add a task at 10:30']").Click();

        Assert.Equal(new TimeOnly(10, 30), asked);
    }

    [Fact]
    public void WithoutAnAddHandlerThereIsNothingToClick()
    {
        var timeline = Render(Timed("A", 9, 0, 10, 0));

        Assert.DoesNotContain(timeline.FindAll("button"), button => button.GetAttribute("aria-label")!.StartsWith("Add a task at"));
    }

    [Fact]
    public void TickingABlockTogglesItsTask()
    {
        TodoTask? toggled = null;
        var task = Timed("A", 9, 0, 10, 0);
        var timeline = Render<DayTimeline>(parameters => Bind(parameters, [task], null).Add(p => p.OnToggle, t => toggled = t));

        timeline.Find("input[type=checkbox]").Change(true);

        Assert.Same(task, toggled);
    }

    [Fact]
    public void TickingAGroupLineTogglesItsTask()
    {
        TodoTask? toggled = null;
        TodoTask[] tasks = [Timed("A", 9, 0, 9, 15), Timed("B", 9, 0, 9, 30), Timed("C", 9, 0, 10, 0)];
        var timeline = Render<DayTimeline>(parameters => Bind(parameters, tasks, null).Add(p => p.OnToggle, t => toggled = t));

        timeline.FindAll("input[type=checkbox]")[1].Change(true);

        Assert.Same(tasks[1], toggled);
    }

    static IReadOnlyList<AngleSharp.Dom.IElement> Folds(IRenderedComponent<DayTimeline> timeline) =>
        [.. timeline.FindAll("button").Where(button => button.GetAttribute("aria-label")!.StartsWith("Show "))];

    IRenderedComponent<DayTimeline> Render(params TodoTask[] tasks) => Render(tasks, null);

    IRenderedComponent<DayTimeline> Render(TodoTask[] tasks, TimeOnly? now) =>
        Render<DayTimeline>(parameters => Bind(parameters, tasks, now));

    static ComponentParameterCollectionBuilder<DayTimeline> Bind(
        ComponentParameterCollectionBuilder<DayTimeline> parameters, TodoTask[] tasks, TimeOnly? now)
    {
        var byId = tasks.ToDictionary(task => task.Id);
        var entries = TodayRule.Plan(tasks, Day, Day, TimeZoneInfo.Utc).Scheduled;
        return parameters
            .Add(p => p.Entries, entries)
            .Add(p => p.Tasks, byId)
            .Add(p => p.Day, Day)
            .Add(p => p.ListNameOf, _ => "Praca")
            .Add(p => p.Now, now);
    }

    static TodoTask Timed(string name, int sh, int sm, int? eh, int? em)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name), DateTimeOffset.UnixEpoch));
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, Day), DateTimeOffset.UnixEpoch));
        task.ApplyAll(TodoTask.Decide(task, new SetTaskTime(Guid.NewGuid(), User, task.Id,
            TaskTime.Of(new TimeOnly(sh, sm), eh is { } h ? new TimeOnly(h, em!.Value) : null)), DateTimeOffset.UnixEpoch));
        return task;
    }

    public DayTimelineTests() => AppTestHost.Arrange(this, User, Day);
}
