using PSPad.App.State;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class CreatedOnTests
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void ItIsTheCreationDayInTheGivenTimeZone()
    {
        var task = Created(new DateTimeOffset(2026, 9, 12, 2, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 9, 12), CreatedOn.Of(task, "Etc/UTC"));
        Assert.Equal(new DateOnly(2026, 9, 11), CreatedOn.Of(task, "America/New_York"));
    }

    [Fact]
    public void AnUnknownTimeZoneFallsBackToUtc()
    {
        var task = Created(new DateTimeOffset(2026, 9, 12, 2, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 9, 12), CreatedOn.Of(task, "Nowhere/Nothing"));
    }

    [Fact]
    public void ATaskWithNoCreationTimeHasNoCreationDay()
    {
        Assert.Null(CreatedOn.Of(new TodoTask(), "Etc/UTC"));
    }

    static TodoTask Created(DateTimeOffset at)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "Buy milk"), at));
        return task;
    }
}
