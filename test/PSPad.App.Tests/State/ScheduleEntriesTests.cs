using PSPad.App.State;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class ScheduleEntriesTests
{
    static readonly TodayEntry A = Entry("A", new TimeOnly(9, 0));
    static readonly TodayEntry B = Entry("B", new TimeOnly(8, 0));
    static readonly TodayEntry C = Entry("C", null);

    [Fact]
    public void WithTheSettingOffOnlyPendingTimedTasksAreDrawn()
    {
        Assert.Equal([A], ScheduleEntries.For(Plan([A], [B, C]), false));
    }

    [Fact]
    public void WithTheSettingOnFinishedTimedTasksStayInStartOrder()
    {
        Assert.Equal([B, A], ScheduleEntries.For(Plan([A], [B, C]), true));
    }

    [Fact]
    public void FinishedTasksWithoutATimeNeverReachTheTimeline()
    {
        Assert.Equal([A], ScheduleEntries.For(Plan([A], [C]), true));
    }

    [Fact]
    public void EqualStartTimesKeepThePendingTaskBeforeTheFinishedOne()
    {
        var pending = Entry("Pending", new TimeOnly(9, 0));
        var finished = Entry("Finished", new TimeOnly(9, 0));

        Assert.Equal([pending, finished], ScheduleEntries.For(Plan([pending], [finished]), true));
    }

    static DayPlan Plan(TodayEntry[] scheduled, TodayEntry[] completed) =>
        new([], scheduled, [], [], [], completed);

    static TodayEntry Entry(string name, TimeOnly? start) =>
        new(Guid.NewGuid(), Guid.NewGuid(), name, false, null, false, false, 0,
            start is { } s ? TaskTime.Of(s, null) : null);
}
