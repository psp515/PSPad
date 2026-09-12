using System.Text.Json;
using PSPad.App.State;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class IndexedDbReplicaTests
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateTimeOffset At = DateTimeOffset.UnixEpoch;

    [Fact]
    public void ADocumentSerialisesWithItsTypeAndOwner()
    {
        var area = NewArea("Home");

        var row = ReplicaRow.From(area);

        Assert.Equal(nameof(Area), row.Type);
        Assert.Equal(User, row.UserId);
        Assert.Equal(area.Id, row.Id);
    }

    [Fact]
    public void ARowRoundTripsBackIntoItsAggregate()
    {
        var area = NewArea("Home");

        var restored = ReplicaRow.From(area).To<Area>();

        Assert.Equal("Home", restored.Name);
        Assert.Equal(area.Version, restored.Version);
        Assert.Equal(area.Seq, restored.Seq);
    }

    [Fact]
    public void ARowFromTheServerRestoresTheSameWay()
    {
        var area = NewArea("Home");
        var fromServer = JsonSerializer.SerializeToElement(area);

        var restored = ReplicaRow.FromServer(nameof(Area), User, area.Id, fromServer).To<Area>();

        Assert.Equal("Home", restored.Name);
    }

    [Fact]
    public void AnInboxKeepsItsItemsThroughTheReplica()
    {
        var inbox = new Inbox();
        var inboxId = Guid.NewGuid();
        inbox.ApplyAll(Inbox.Decide(null, new CreateInbox(Guid.NewGuid(), User, inboxId), At));
        inbox.ApplyAll(Inbox.Decide(
            inbox, new CaptureToInbox(Guid.NewGuid(), User, inbox.Id, Guid.NewGuid(), "Call the plumber"), At));

        var restored = ReplicaRow.From(inbox).To<Inbox>();

        Assert.Equal(["Call the plumber"], restored.Items.Select(item => item.Text));
    }

    [Fact]
    public void ATaskKeepsItsStepsThroughTheReplica()
    {
        var task = NewTask("Buy milk");
        task.ApplyAll(TodoTask.Decide(
            task, new AddStep(Guid.NewGuid(), User, task.Id, Guid.NewGuid(), "Find the shop"), At));

        var restored = ReplicaRow.From(task).To<TodoTask>();

        Assert.Equal(["Find the shop"], restored.Steps.Select(step => step.Name));
    }

    [Fact]
    public void ARecurringTaskKeepsTheDaysItWasCompletedOn()
    {
        var day = new DateOnly(2026, 9, 12);
        var task = NewTask("Read a book");
        task.ApplyAll(TodoTask.Decide(
            task, new SetTaskRecurrence(Guid.NewGuid(), User, task.Id, RecurrenceRule.Daily(day)), At));
        task.ApplyAll(TodoTask.Decide(
            task, new CompleteOccurrence(Guid.NewGuid(), User, task.Id, day, true), At));

        var restored = ReplicaRow.From(task).To<TodoTask>();

        Assert.Contains(day, restored.CompletedDays);
    }

    static Area NewArea(string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, 0), DateTimeOffset.UnixEpoch));
        return area;
    }

    static TodoTask NewTask(string name)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), name), At));
        return task;
    }
}
