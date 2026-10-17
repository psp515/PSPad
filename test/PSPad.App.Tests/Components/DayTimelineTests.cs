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
    public void HoursRunFromTheFirstStartToTheLastEnd()
    {
        var timeline = Render(Timed("Sprint planning", 9, 30, 11, 0), Timed("Standup", 12, 0, 12, 15));

        var hours = timeline.FindAll(".pspad-timeline-hour");
        Assert.Equal(4, hours.Count);
        Assert.Equal("09:00", hours[0].TextContent.Trim());
        Assert.Equal("12:00", hours[^1].TextContent.Trim());
    }

    [Fact]
    public void ABlockIsPlacedByItsStartAndSizedByItsLength()
    {
        var timeline = Render(Timed("Sprint planning", 9, 30, 11, 0));

        var block = timeline.Find(".pspad-timeline-task");
        var style = block.GetAttribute("style")!;
        Assert.Contains("top:28px", style);
        Assert.Contains("height:82px", style);
        Assert.Equal("09:30–11:00", block.QuerySelector(".pspad-schedule-time")!.TextContent.Trim());
        Assert.Contains("Sprint planning", block.TextContent);
        Assert.Contains("Praca", block.TextContent);
    }

    [Fact]
    public void ATaskWithoutAnEndTakesHalfAnHour()
    {
        var timeline = Render(Timed("Standup", 9, 30, null, null));

        var block = timeline.Find(".pspad-timeline-task");
        Assert.Contains("height:26px", block.GetAttribute("style"));
        Assert.Equal("09:30", block.QuerySelector(".pspad-schedule-start")!.TextContent);
        Assert.Empty(block.QuerySelectorAll(".pspad-schedule-end"));
    }

    [Fact]
    public void AnOvernightTaskEndsAtMidnightAndIsMarked()
    {
        var timeline = Render(Timed("Night shift", 22, 0, 1, 0));

        Assert.Equal(2, timeline.FindAll(".pspad-timeline-hour").Count);
        Assert.Contains("height:110px", timeline.Find(".pspad-timeline-task").GetAttribute("style"));
        Assert.Equal("(+1)", timeline.Find(".pspad-schedule-nextday").TextContent.Trim());
    }

    [Fact]
    public void OverlappingTasksShareTheWidthInLanes()
    {
        var timeline = Render(Timed("A", 9, 0, 10, 0), Timed("B", 9, 30, 10, 30), Timed("C", 11, 0, 12, 0));

        var blocks = timeline.FindAll(".pspad-timeline-task");
        Assert.Contains("--lane:0;--lanes:2", blocks[0].GetAttribute("style"));
        Assert.Contains("--lane:1;--lanes:2", blocks[1].GetAttribute("style"));
        Assert.Contains("--lane:0;--lanes:1", blocks[2].GetAttribute("style"));
    }

    [Fact]
    public void TheNowLineShowsOnlyWhenNowIsGiven()
    {
        var without = Render(Timed("A", 9, 0, 10, 0));
        Assert.Empty(without.FindAll(".pspad-timeline-now"));

        var with = Render([Timed("A", 9, 0, 10, 0)], new TimeOnly(9, 15));
        Assert.Contains("top:14px", with.Find(".pspad-timeline-now").GetAttribute("style"));
    }

    [Fact]
    public void TickingABlockTogglesItsTask()
    {
        TodoTask? toggled = null;
        var task = Timed("A", 9, 0, 10, 0);
        var timeline = Render<DayTimeline>(parameters => Bind(parameters, [task], null).Add(p => p.OnToggle, t => toggled = t));

        timeline.Find(".pspad-timeline-task input.mud-checkbox-input").Change(true);

        Assert.Same(task, toggled);
    }

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
