using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Today;

[UnitTest]
public class ReminderRuleTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset LongAgo = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
    static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    static TodoTask Task(
        DateOnly? due, TaskReminder? reminder, DateTimeOffset? setAt = null, RecurrenceRule? repeat = null)
    {
        var task = new TodoTask();
        var id = Guid.NewGuid();
        task.Apply(new TaskCreated(id, User, LongAgo, Guid.NewGuid(), "Pay rent"));

        if (due is not null)
        {
            task.Apply(new TaskDueDateSet(id, User, LongAgo, due));
        }

        if (repeat is not null)
        {
            task.Apply(new TaskRecurrenceSet(id, User, LongAgo, repeat));
        }

        if (reminder is not null)
        {
            task.Apply(new TaskReminderSet(id, User, setAt ?? LongAgo, reminder));
        }

        return task;
    }

    [Fact]
    public void AOneTimeTaskFiresOnItsDueDayAtItsHour()
    {
        var task = Task(new DateOnly(2026, 11, 20), TaskReminder.Of(0, new TimeOnly(9, 0)), Utc(2026, 11, 1, 0, 0));

        var moments = ReminderRule.MomentsBetween(task, Warsaw, Utc(2026, 11, 20, 7, 59), Utc(2026, 11, 20, 8, 0));

        var moment = Assert.Single(moments);
        Assert.Equal(Utc(2026, 11, 20, 8, 0), moment.Moment);
        Assert.Equal(new DateOnly(2026, 11, 20), moment.Occurrence);
    }

    [Fact]
    public void DaysBeforeMoveTheMomentEarlier()
    {
        var task = Task(new DateOnly(2026, 11, 20), TaskReminder.Of(3, new TimeOnly(18, 0)));

        var moments = ReminderRule.MomentsBetween(task, Warsaw, Utc(2026, 11, 17, 0, 0), Utc(2026, 11, 18, 0, 0));

        var moment = Assert.Single(moments);
        Assert.Equal(Utc(2026, 11, 17, 17, 0), moment.Moment);
        Assert.Equal(new DateOnly(2026, 11, 20), moment.Occurrence);
    }

    [Fact]
    public void TheWindowIsOpenAtItsStartAndClosedAtItsEnd()
    {
        var task = Task(new DateOnly(2026, 11, 20), TaskReminder.Of(0, new TimeOnly(9, 0)));
        var moment = Utc(2026, 11, 20, 8, 0);

        Assert.Single(ReminderRule.MomentsBetween(task, Warsaw, moment.AddMinutes(-5), moment));
        Assert.Empty(ReminderRule.MomentsBetween(task, Warsaw, moment, moment.AddMinutes(5)));
    }

    [Fact]
    public void ARepeatingTaskFiresForEachPendingOccurrence()
    {
        var task = Task(null, TaskReminder.Of(0, new TimeOnly(7, 0)), repeat: RecurrenceRule.Daily(new DateOnly(2026, 11, 1)));

        var moments = ReminderRule.MomentsBetween(task, TimeZoneInfo.Utc, Utc(2026, 11, 10, 0, 0), Utc(2026, 11, 12, 23, 59));

        Assert.Equal(
            [new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 11), new DateOnly(2026, 11, 12)],
            moments.Select(m => m.Occurrence));
    }

    [Fact]
    public void ACompletedOccurrenceDoesNotFire()
    {
        var task = Task(null, TaskReminder.Of(0, new TimeOnly(7, 0)), repeat: RecurrenceRule.Daily(new DateOnly(2026, 11, 1)));
        task.Apply(new OccurrenceCompleted(task.Id, User, LongAgo, new DateOnly(2026, 11, 11), true, "Pay rent", task.ListId));

        var moments = ReminderRule.MomentsBetween(task, TimeZoneInfo.Utc, Utc(2026, 11, 10, 0, 0), Utc(2026, 11, 12, 23, 59));

        Assert.Equal([new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 12)], moments.Select(m => m.Occurrence));
    }

    [Fact]
    public void ACompletedOneTimeTaskDoesNotFire()
    {
        var task = Task(new DateOnly(2026, 11, 20), TaskReminder.Of(0, new TimeOnly(9, 0)));
        task.Apply(new TaskCompleted(task.Id, User, LongAgo, "Pay rent", task.ListId, new DateOnly(2026, 11, 20)));

        Assert.Empty(ReminderRule.MomentsBetween(task, Warsaw, Utc(2026, 11, 20, 0, 0), Utc(2026, 11, 21, 0, 0)));
    }

    [Fact]
    public void ATaskWithoutDueDateOrRepeatIsDormant()
    {
        var reminder = TaskReminder.Of(0, new TimeOnly(9, 0));
        var dormant = Task(null, reminder);

        Assert.Empty(ReminderRule.MomentsBetween(dormant, Warsaw, Utc(2026, 11, 1, 0, 0), Utc(2026, 12, 1, 0, 0)));
        Assert.True(ReminderRule.IsDormant(dormant));
        Assert.False(ReminderRule.IsDormant(Task(new DateOnly(2026, 11, 20), reminder)));
        Assert.False(ReminderRule.IsDormant(Task(null, null)));
    }

    [Fact]
    public void AMomentBeforeTheReminderWasSetDoesNotFire()
    {
        var task = Task(new DateOnly(2026, 11, 20), TaskReminder.Of(0, new TimeOnly(9, 0)), Utc(2026, 11, 20, 8, 30));

        Assert.Empty(ReminderRule.MomentsBetween(task, Warsaw, Utc(2026, 11, 20, 7, 0), Utc(2026, 11, 20, 12, 0)));
    }

    [Fact]
    public void EachRecipientsZoneGivesItsOwnMoment()
    {
        var task = Task(new DateOnly(2026, 11, 20), TaskReminder.Of(0, new TimeOnly(9, 0)));
        var from = Utc(2026, 11, 19, 0, 0);
        var to = Utc(2026, 11, 21, 0, 0);

        Assert.Equal(Utc(2026, 11, 20, 8, 0), Assert.Single(ReminderRule.MomentsBetween(task, Warsaw, from, to)).Moment);
        Assert.Equal(Utc(2026, 11, 20, 14, 0), Assert.Single(ReminderRule.MomentsBetween(task, NewYork, from, to)).Moment);
    }

    [Fact]
    public void ASkippedSpringHourFiresAtTheFirstValidMinute()
    {
        var moment = ReminderRule.MomentOf(new DateOnly(2027, 3, 28), new TimeOnly(2, 30), Warsaw);

        Assert.Equal(Utc(2027, 3, 28, 1, 0), moment);
    }

    [Fact]
    public void ARepeatedAutumnHourFiresTheFirstTime()
    {
        var moment = ReminderRule.MomentOf(new DateOnly(2026, 10, 25), new TimeOnly(2, 30), Warsaw);

        Assert.Equal(Utc(2026, 10, 25, 0, 30), moment);
    }

    [Fact]
    public void AWindowAcrossMidnightFindsTheMoment()
    {
        var task = Task(new DateOnly(2026, 11, 20), TaskReminder.Of(0, new TimeOnly(23, 59)));

        Assert.Single(ReminderRule.MomentsBetween(task, TimeZoneInfo.Utc, Utc(2026, 11, 20, 23, 58), Utc(2026, 11, 21, 0, 1)));
    }

    [Fact]
    public void ADeletedTaskDoesNotFire()
    {
        var task = Task(new DateOnly(2026, 11, 20), TaskReminder.Of(0, new TimeOnly(9, 0)));
        task.Apply(new TaskDeleted(task.Id, User, LongAgo, "Pay rent"));

        Assert.Empty(ReminderRule.MomentsBetween(task, Warsaw, Utc(2026, 11, 20, 0, 0), Utc(2026, 11, 21, 0, 0)));
    }

    [Fact]
    public void ARepeatEndedByItsDueDateDoesNotFireAfterIt()
    {
        var task = Task(
            new DateOnly(2026, 11, 10), TaskReminder.Of(0, new TimeOnly(7, 0)),
            repeat: RecurrenceRule.Daily(new DateOnly(2026, 11, 1)));

        var moments = ReminderRule.MomentsBetween(task, TimeZoneInfo.Utc, Utc(2026, 11, 9, 0, 0), Utc(2026, 11, 12, 23, 59));

        Assert.Equal([new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 10)], moments.Select(m => m.Occurrence));
    }
}
