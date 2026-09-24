using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Statistics.Tests;

[UnitTest]
public class DomainEventCatalogueTests
{
    static readonly DateTimeOffset At = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AStoredEventComesBackAsItsType()
    {
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var original = new TaskCreated(taskId, userId, At, Guid.NewGuid(), "Fix the sink");
        var recorded = new RecordedEvent(
            7, "TodoTask", taskId, "TaskCreated",
            JsonSerializer.Serialize(original), At);

        var revived = Assert.IsType<TaskCreated>(DomainEventCatalogue.Deserialize(recorded));

        Assert.Equal("Fix the sink", revived.Name);
    }

    [Fact]
    public void AnEventFromAModuleStatisticsDoesNotKnowIsSkipped()
    {
        var recorded = new RecordedEvent(
            7, "User", Guid.NewGuid(), "UserProvisioned", "{}", At);

        Assert.Null(DomainEventCatalogue.Deserialize(recorded));
    }

    [Fact]
    public void AKnownEventTypeResolvesToItsClrType()
    {
        Assert.Equal(typeof(TaskCreated), DomainEventCatalogue.Resolve("TaskCreated"));
    }
}
