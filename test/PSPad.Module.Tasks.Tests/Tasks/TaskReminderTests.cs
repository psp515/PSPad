using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Tasks;

[UnitTest]
public class TaskReminderTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AReminderIsSetWithItsMomentAndCleared()
    {
        var task = TodoTaskTests.Existing();

        task.ApplyAll(TodoTask.Decide(task, Set(task, TaskReminder.Of(1, new TimeOnly(9, 0))), Now));
        Assert.Equal(new TaskReminder(1, new TimeOnly(9, 0)), task.Reminder);
        Assert.Equal(Now, task.ReminderSetAt);

        task.ApplyAll(TodoTask.Decide(task, Set(task, null), Now.AddHours(1)));
        Assert.Null(task.Reminder);
    }

    [Fact]
    public void ANewTaskHasNoReminder() => Assert.Null(TodoTaskTests.Existing().Reminder);

    [Fact]
    public void SettingTheSameReminderAgainRecordsNothing()
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(task, Set(task, TaskReminder.Of(0, new TimeOnly(8, 30))), Now));

        Assert.Empty(TodoTask.Decide(task, Set(task, new TaskReminder(0, new TimeOnly(8, 30))), Now));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void AReminderOutsideItsBoundsIsRejected(int daysBefore)
    {
        var task = TodoTaskTests.Existing();

        var rejection = Assert.Throws<DomainRejectedException>(() =>
            TodoTask.Decide(task, Set(task, new TaskReminder(daysBefore, new TimeOnly(9, 0))), Now));
        Assert.Equal("A reminder must be between 0 and 99 days before.", rejection.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void TheBoundsThemselvesAreAccepted(int daysBefore) =>
        Assert.Equal(daysBefore, TaskReminder.Of(daysBefore, new TimeOnly(9, 0)).DaysBefore);

    [Fact]
    public void AMissingTaskIsRejected() =>
        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(
            null, new SetTaskReminder(Guid.NewGuid(), User, Guid.NewGuid(), TaskReminder.Of(1, new TimeOnly(9, 0))), Now));

    [Fact]
    public void AnUndatedTaskAcceptsADormantReminder()
    {
        var task = TodoTaskTests.Existing();

        Assert.Single(TodoTask.Decide(task, Set(task, TaskReminder.Of(2, new TimeOnly(9, 0))), Now));
    }

    static SetTaskReminder Set(TodoTask task, TaskReminder? reminder) => new(Guid.NewGuid(), User, task.Id, reminder);
}
