using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Tasks;

[UnitTest]
public class TaskLeadTimeTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ALeadTimeIsSetAndCleared()
    {
        var task = TodoTaskTests.Existing();

        task.ApplyAll(TodoTask.Decide(task, Set(task, LeadTime.Of(2, LeadUnit.Weeks)), Now));
        Assert.Equal(new LeadTime(2, LeadUnit.Weeks), task.LeadTime);

        task.ApplyAll(TodoTask.Decide(task, Set(task, null), Now));
        Assert.Null(task.LeadTime);
    }

    [Fact]
    public void SettingTheSameLeadTimeAgainRecordsNothing()
    {
        var task = TodoTaskTests.Existing();
        task.ApplyAll(TodoTask.Decide(task, Set(task, LeadTime.Of(3, LeadUnit.Days)), Now));

        Assert.Empty(TodoTask.Decide(task, Set(task, new LeadTime(3, LeadUnit.Days)), Now));
    }

    [Theory]
    [InlineData(0, LeadUnit.Days)]
    [InlineData(100, LeadUnit.Days)]
    [InlineData(1, (LeadUnit)7)]
    public void AHandBuiltLeadTimeOutsideItsBoundsIsRejected(int amount, LeadUnit unit)
    {
        var task = TodoTaskTests.Existing();

        Assert.Throws<DomainRejectedException>(() =>
            TodoTask.Decide(task, Set(task, new LeadTime(amount, unit)), Now));
    }

    [Fact]
    public void AMissingTaskIsRejected()
    {
        Assert.Throws<DomainRejectedException>(() => TodoTask.Decide(
            null, new SetTaskLeadTime(Guid.NewGuid(), User, Guid.NewGuid(), LeadTime.Of(1, LeadUnit.Days)), Now));
    }

    [Fact]
    public void AnUndatedTaskAcceptsALeadTime()
    {
        var task = TodoTaskTests.Existing();

        Assert.Single(TodoTask.Decide(task, Set(task, LeadTime.Of(1, LeadUnit.Months)), Now));
    }

    static SetTaskLeadTime Set(TodoTask task, LeadTime? lead) =>
        new(Guid.NewGuid(), User, task.Id, lead);
}
