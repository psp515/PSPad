using PSPad.App.State;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class ScheduleEntriesTests
{
    static readonly DateOnly Day = new(2026, 11, 10);
    static readonly TodayEntry A = Entry("A", new TimeOnly(9, 0));
    static readonly TodayEntry B = Entry("B", new TimeOnly(8, 0));
    static readonly TodayEntry C = Entry("C", null);

    [Fact]
    public void WithTheSettingOffOnlyPendingTimedTasksAreDrawn()
    {
        Assert.Equal([A], ScheduleEntries.For(Plan([A], [B, C]), Day, false));
    }

    [Fact]
    public void WithTheSettingOnFinishedTimedTasksStayInStartOrder()
    {
        Assert.Equal([B, A], ScheduleEntries.For(Plan([A], [B, C]), Day, true));
    }

    [Fact]
    public void FinishedTasksWithoutATimeNeverReachTheTimeline()
    {
        Assert.Equal([A], ScheduleEntries.For(Plan([A], [C]), Day, true));
    }

    [Fact]
    public void TickingATaskWithAnIdenticalSpanLeavesTheOrderUnchanged()
    {
        var first = Entry("A", new TimeOnly(9, 0));
        var second = Entry("B", new TimeOnly(9, 0));

        var before = ScheduleEntries.For(Plan([first, second], []), Day, true);
        var after = ScheduleEntries.For(Plan([second], [first]), Day, true);

        Assert.Equal([first, second], before);
        Assert.Equal(before, after);
    }

    [Fact]
    public void TickingARecurringTaskWithAnIdenticalSpanLeavesTheOrderUnchanged()
    {
        var first = Entry("A", new TimeOnly(9, 0), recurring: true);
        var second = Entry("B", new TimeOnly(9, 0), recurring: true);

        var before = ScheduleEntries.For(Plan([first, second], []), Day, true);
        var after = ScheduleEntries.For(Plan([second], [first]), Day, true);

        Assert.Equal([first, second], before);
        Assert.Equal(before, after);
    }

    [Fact]
    public void AnOverdueTaskFinishedTodayIsNotDrawn()
    {
        var overdue = Entry("Overdue", new TimeOnly(9, 0), Day.AddDays(-3));

        Assert.Empty(ScheduleEntries.For(Plan([], [overdue]), Day, true));
    }

    [Fact]
    public void ATaskDueAnotherDayFinishedEarlyIsNotDrawn()
    {
        var early = Entry("Early", new TimeOnly(9, 0), Day.AddDays(2));

        Assert.Empty(ScheduleEntries.For(Plan([], [early]), Day, true));
    }

    static DayPlan Plan(TodayEntry[] scheduled, TodayEntry[] completed) =>
        new([], scheduled, [], [], [], completed);

    static TodayEntry Entry(string name, TimeOnly? start, DateOnly? dueOn = null, bool recurring = false) =>
        new(Guid.NewGuid(), Guid.NewGuid(), name, false, dueOn ?? Day, recurring, false, 0,
            start is { } s ? TaskTime.Of(s, null) : null);
}
