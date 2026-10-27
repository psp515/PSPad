using Bunit;
using PSPad.App.Components;
using PSPad.App.State;
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

        Assert.Equal(["00:00–09:00", "12:00–24:00"],
            timeline.FindAll(".pspad-timeline-fold").Select(fold => fold.TextContent.Trim()));
        Assert.Equal(["09:00", "10:00", "11:00"],
            timeline.FindAll(".pspad-timeline-hour-label").Select(label => label.TextContent.Trim()));
    }

    [Fact]
    public void AnEmptyDayIsOneBand()
    {
        var timeline = Render();

        Assert.Equal("00:00–24:00", timeline.Find(".pspad-timeline-fold").TextContent.Trim());
        Assert.Empty(timeline.FindAll(".pspad-timeline-hour"));
    }

    [Fact]
    public void ClickingABandShowsItsHours()
    {
        var timeline = Render(Timed("Sprint planning", 9, 30, 11, 30));

        timeline.FindAll(".pspad-timeline-fold")[0].Click();

        var labels = timeline.FindAll(".pspad-timeline-hour-label").Select(label => label.TextContent.Trim()).ToArray();
        Assert.Equal(12, labels.Length);
        Assert.Equal("00:00", labels[0]);
        Assert.Single(timeline.FindAll(".pspad-timeline-fold"));
    }

    [Fact]
    public void TheTimelineAsksForOnlyTheLengthItsDayNeeds()
    {
        var timeline = Render(Timed("Dentist", 15, 0, 16, 0));

        var natural = Axis((900, 960)).NaturalLength(DayTimeline.HourLength);
        Assert.Contains($"--pspad-timeline-natural:{natural}px", timeline.Find(".pspad-timeline").GetAttribute("style"));
    }

    [Fact]
    public void ABlockIsPlacedOnTheAxisAndSizedByItsLength()
    {
        var timeline = Render(Timed("Sprint planning", 9, 30, 11, 30));

        var axis = Axis((570, 690));
        var block = timeline.Find(".pspad-timeline-task");
        var style = block.GetAttribute("style")!;
        Assert.Contains($"--at:{axis.At(570)}", style);
        Assert.Contains($"--size:{axis.Stretch(570, 690)}", style);
        Assert.Equal("09:30–11:30", block.QuerySelector(".pspad-schedule-time")!.TextContent.Trim());
        Assert.Equal("Sprint planning – 09:30–11:30", block.GetAttribute("title"));
        Assert.Contains("Sprint planning", block.TextContent);
        Assert.Contains("Praca", block.TextContent);
        Assert.DoesNotContain("pspad-timeline-task-short", block.ClassList);
    }

    [Fact]
    public void ABlockUnderTwoHoursLeavesOutItsListAndUnderNinetyMinutesIsOneLine()
    {
        var timeline = Render(Timed("Review", 9, 0, 10, 30), Timed("Call", 12, 0, 13, 15));

        var blocks = timeline.FindAll(".pspad-timeline-task");
        Assert.Empty(blocks[0].QuerySelectorAll(".pspad-timeline-task-list"));
        Assert.DoesNotContain("pspad-timeline-task-short", blocks[0].ClassList);
        Assert.Contains("pspad-timeline-task-short", blocks[1].ClassList);
    }

    [Fact]
    public void AShortBlockBesideAnotherShowsOnlyItsStart()
    {
        var timeline = Render(Timed("Pay rent", 13, 0, 13, 10), Timed("Vitamins", 13, 15, null, null), Timed("Gym", 18, 0, 18, 30));

        var blocks = timeline.FindAll(".pspad-timeline-task");
        Assert.Contains("pspad-timeline-task-narrow", blocks[0].ClassList);
        Assert.DoesNotContain("pspad-timeline-task-narrow", blocks[2].ClassList);
    }

    [Fact]
    public void ATaskWithoutAnEndTakesHalfAnHourOnOneLine()
    {
        var timeline = Render(Timed("Standup", 9, 30, null, null));

        var block = timeline.Find(".pspad-timeline-task");
        Assert.Contains($"--size:{Axis((570, 600)).Stretch(570, 600)}", block.GetAttribute("style"));
        Assert.Contains("pspad-timeline-task-short", block.ClassList);
        Assert.Equal("09:30", block.QuerySelector(".pspad-schedule-start")!.TextContent);
        Assert.Empty(block.QuerySelectorAll(".pspad-schedule-end"));
    }

    [Fact]
    public void AnOvernightTaskEndsAtMidnightAndIsMarked()
    {
        var timeline = Render(Timed("Night shift", 22, 0, 1, 0));

        Assert.Contains($"--size:{Axis((1320, 1440)).Stretch(1320, 1440)}", timeline.Find(".pspad-timeline-task").GetAttribute("style"));
        Assert.Equal("(+1)", timeline.Find(".pspad-schedule-nextday").TextContent.Trim());
    }

    [Fact]
    public void TwoOverlappingTasksShareTheWidthInLanes()
    {
        var timeline = Render(Timed("A", 9, 0, 10, 0), Timed("B", 9, 30, 10, 30), Timed("C", 11, 0, 12, 0));

        var blocks = timeline.FindAll(".pspad-timeline-task");
        Assert.Contains("--lane:0;--lanes:2", blocks[0].GetAttribute("style"));
        Assert.Contains("--lane:1;--lanes:2", blocks[1].GetAttribute("style"));
        Assert.Contains("--lane:0;--lanes:1", blocks[2].GetAttribute("style"));
    }

    [Fact]
    public void ShortTasksThatWouldTouchOnScreenTakeSeparateLanes()
    {
        var timeline = Render(Timed("Pay rent", 13, 0, 13, 10), Timed("Vitamins", 13, 15, null, null));

        var blocks = timeline.FindAll(".pspad-timeline-task");
        Assert.Contains("--lane:0;--lanes:2", blocks[0].GetAttribute("style"));
        Assert.Contains("--lane:1;--lanes:2", blocks[1].GetAttribute("style"));
    }

    [Fact]
    public void ThreeOverlappingTasksBecomeOneGroupOfLines()
    {
        var timeline = Render(Timed("Standup", 9, 0, 9, 15), Timed("Call", 9, 0, 9, 30), Timed("Review", 9, 0, 10, 0));

        Assert.Empty(timeline.FindAll(".pspad-timeline-task"));
        var group = timeline.Find(".pspad-timeline-group");
        var style = group.GetAttribute("style")!;
        Assert.Contains($"--at:{Axis((540, 555), (540, 570), (540, 600)).At(540)}", style);
        Assert.Contains("--needs:76px", style);
        Assert.Contains("--lane:0;--lanes:1", style);
        Assert.DoesNotContain("pspad-timeline-task-narrow", group.ClassList);
        var lines = group.QuerySelectorAll(".pspad-timeline-group-line");
        Assert.Equal(["Call", "Review", "Standup"], lines.Select(line => line.QuerySelector(".pspad-timeline-task-name")!.TextContent.Trim()));
        Assert.Equal("09:00–09:30", lines[0].QuerySelector(".pspad-schedule-time")!.TextContent.Trim());
    }

    [Fact]
    public void ALongTaskKeepsItsColumnWhileTheTasksBesideItKeepTheirOwnTimes()
    {
        var timeline = Render(Timed("Workday", 10, 0, 18, 0), Timed("Call", 11, 0, null, null),
            Timed("Review", 16, 0, 17, 0), Timed("Sync", 16, 15, 17, 0), Timed("Demo", 16, 45, 17, 15));

        var blocks = timeline.FindAll(".pspad-timeline-task");
        Assert.Equal(["Workday", "Call"], blocks.Select(block => block.QuerySelector(".pspad-timeline-task-name")!.TextContent.Trim()));
        Assert.Contains("--lane:0;--lanes:2", blocks[0].GetAttribute("style"));
        Assert.Contains("--lane:1;--lanes:2", blocks[1].GetAttribute("style"));
        var group = timeline.Find(".pspad-timeline-group");
        Assert.Contains("--lane:1;--lanes:2", group.GetAttribute("style"));
        Assert.Contains("pspad-timeline-task-narrow", group.ClassList);
        Assert.Equal(["Review", "Sync", "Demo"],
            group.QuerySelectorAll(".pspad-timeline-task-name").Select(name => name.TextContent.Trim()));
    }

    [Fact]
    public void TheNowLineShowsOnlyWhenNowIsGiven()
    {
        var without = Render(Timed("A", 9, 0, 10, 0));
        Assert.Empty(without.FindAll(".pspad-timeline-now"));

        var with = Render([Timed("A", 9, 0, 10, 0)], new TimeOnly(9, 15));
        var axis = TimelineAxis.For([(540, 600)], 555, new HashSet<int>());
        Assert.Contains($"--at:{axis.At(555)}", with.Find(".pspad-timeline-now").GetAttribute("style"));
    }

    [Fact]
    public void ClickingAnEmptyHalfHourAsksForATaskThere()
    {
        TimeOnly? asked = null;
        var timeline = Render<DayTimeline>(parameters =>
            Bind(parameters, [Timed("A", 9, 0, 10, 0), Timed("B", 11, 0, 12, 0)], null).Add(p => p.OnAdd, time => asked = time));

        timeline.Find(".pspad-timeline-slot[data-time='10:30']").Click();

        Assert.Equal(new TimeOnly(10, 30), asked);
    }

    [Fact]
    public void WithoutAnAddHandlerThereIsNothingToClick()
    {
        var timeline = Render(Timed("A", 9, 0, 10, 0));

        Assert.Empty(timeline.FindAll(".pspad-timeline-slot"));
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

    [Fact]
    public void TickingAGroupLineTogglesItsTask()
    {
        TodoTask? toggled = null;
        TodoTask[] tasks = [Timed("A", 9, 0, 9, 15), Timed("B", 9, 0, 9, 30), Timed("C", 9, 0, 10, 0)];
        var timeline = Render<DayTimeline>(parameters => Bind(parameters, tasks, null).Add(p => p.OnToggle, t => toggled = t));

        timeline.FindAll(".pspad-timeline-group-line input.mud-checkbox-input")[1].Change(true);

        Assert.Same(tasks[1], toggled);
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

    static TimelineAxis Axis(params (int Start, int End)[] spans) => TimelineAxis.For(spans, null, new HashSet<int>());

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
