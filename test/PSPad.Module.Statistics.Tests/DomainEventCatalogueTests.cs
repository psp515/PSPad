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

    [Fact]
    public void AnOldNarrowTaskCompletedPayloadStillReplays()
    {
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var oldPayload = JsonSerializer.Serialize(new { AggregateId = taskId, UserId = userId, At });
        var recorded = new RecordedEvent(7, "TodoTask", taskId, "TaskCompleted", oldPayload, At);

        var revived = Assert.IsType<TaskCompleted>(DomainEventCatalogue.Deserialize(recorded));

        Assert.Equal(taskId, revived.AggregateId);
        Assert.Equal(userId, revived.UserId);
        Assert.Null(revived.Name);
        Assert.Equal(Guid.Empty, revived.ListId);
        Assert.Null(revived.GoalId);
        Assert.Null(revived.DueOn);
    }

    [Fact]
    public void AnOldNarrowTaskDeletedPayloadStillReplays()
    {
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var oldPayload = JsonSerializer.Serialize(new { AggregateId = taskId, UserId = userId, At });
        var recorded = new RecordedEvent(7, "TodoTask", taskId, "TaskDeleted", oldPayload, At);

        var revived = Assert.IsType<TaskDeleted>(DomainEventCatalogue.Deserialize(recorded));

        Assert.Null(revived.Name);
    }
}
