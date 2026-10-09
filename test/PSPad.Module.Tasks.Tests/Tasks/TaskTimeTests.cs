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

    [Fact]
    public void AnEndEqualToTheStartIsRejected()
    {
        var task = TodoTaskTests.Existing();

        var rejection = Assert.Throws<DomainRejectedException>(() => TaskTime.Of(NineThirty, NineThirty));
        Assert.Equal("A task can't end when it starts.", rejection.Message);
        Assert.Throws<DomainRejectedException>(() =>
            TodoTask.Decide(task, Set(task, new TaskTime(NineThirty, NineThirty)), Now));
    }

    [Fact]
    public void AnEndBeforeTheStartRunsOvernight()
    {
        var task = TodoTaskTests.Existing();
        var overnight = TaskTime.Of(new TimeOnly(22, 0), new TimeOnly(1, 0));

        task.ApplyAll(TodoTask.Decide(task, Set(task, overnight), Now));

        Assert.Equal(overnight, task.Time);
        Assert.True(overnight.Overnight);
    }

    [Theory]
    [InlineData(9, 30, 11, 0)]
    [InlineData(9, 30, -1, 0)]
    public void ATimeWithinTheDayIsNotOvernight(int startHour, int startMinute, int endHour, int endMinute) =>
        Assert.False(new TaskTime(new TimeOnly(startHour, startMinute),
            endHour < 0 ? null : new TimeOnly(endHour, endMinute)).Overnight);

    [Fact]
    public void AMissingTaskIsRejected()
    {
        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(
            null, new SetTaskTime(Guid.NewGuid(), User, Guid.NewGuid(), TaskTime.Of(NineThirty, null)), Now));
    }

    static SetTaskTime Set(TodoTask task, TaskTime? time) => new(Guid.NewGuid(), User, task.Id, time);
}
