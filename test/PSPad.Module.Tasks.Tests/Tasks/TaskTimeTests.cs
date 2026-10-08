using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Tasks;

[UnitTest]
public class TaskTimeTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 10, 8, 8, 0, 0, TimeSpan.Zero);
    static readonly TimeOnly NineThirty = new(9, 30);
    static readonly TimeOnly Eleven = new(11, 0);

    [Fact]
    public void ATimeIsSetAndCleared()
    {
        var task = TodoTaskTests.Existing();

        task.ApplyAll(TodoTask.Decide(task, Set(task, TaskTime.Of(NineThirty, Eleven)), Now));
        Assert.Equal(new TaskTime(NineThirty, Eleven), task.Time);

        task.ApplyAll(TodoTask.Decide(task, Set(task, null), Now));
        Assert.Null(task.Time);
    }

    [Fact]
    public void AStartWithoutAnEndIsAccepted()
    {
        var task = TodoTaskTests.Existing();

        task.ApplyAll(TodoTask.Decide(task, Set(task, TaskTime.Of(NineThirty, null)), Now));

        Assert.Equal(new TaskTime(NineThirty, null), task.Time);
    }

    [Fact]
    public void SettingTheSameTimeAgainRecordsNothing()
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(task, Set(task, TaskTime.Of(NineThirty, Eleven)), Now));

        Assert.Empty(TodoTask.Decide(task, Set(task, new TaskTime(NineThirty, Eleven)), Now));
    }

    [Theory]
    [InlineData(11, 0, 9, 30)]
    [InlineData(9, 30, 9, 30)]
    public void AnEndThatIsNotAfterTheStartIsRejected(int startHour, int startMinute, int endHour, int endMinute)
    {
        var start = new TimeOnly(startHour, startMinute);
        var end = new TimeOnly(endHour, endMinute);
        var task = TodoTaskTests.Existing();

        var rejection = Assert.Throws<DomainRejectedException>(() => TaskTime.Of(start, end));
        Assert.Equal("A task must end after it starts.", rejection.Message);
        Assert.Throws<DomainRejectedException>(() =>
            TodoTask.Decide(task, Set(task, new TaskTime(start, end)), Now));
    }

    [Fact]
    public void AMissingTaskIsRejected()
    {
        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(
            null, new SetTaskTime(Guid.NewGuid(), User, Guid.NewGuid(), TaskTime.Of(NineThirty, null)), Now));
    }

    static SetTaskTime Set(TodoTask task, TaskTime? time) => new(Guid.NewGuid(), User, task.Id, time);
}
